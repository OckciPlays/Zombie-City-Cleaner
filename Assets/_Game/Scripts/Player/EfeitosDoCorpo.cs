using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Video;
using ZombieCityCleaner.World;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O que o corpo machucado faz no dono (05/11/2026, o sistema de vida por membro, a etapa 1), sem mexer no código do
    /// movimento: a cada quadro, escreve nos campos públicos do <see cref="MovimentoPrimeiraPessoa"/> o valor original
    /// vezes o fator das pernas (<see cref="PartesDoCorpo.FatorDeAndar"/>; ninguém mais escreve neles durante o jogo), sem
    /// correr com a perna torcida, quebrada ou inutilizada (<see cref="PartesDoCorpo.SemCorrer"/>: o correr fica igual ao
    /// andar) e com o pulo que sobra (<see cref="PartesDoCorpo.PuloQueSobra"/>).
    /// <para>
    /// O mancar forte (a perna quebrada sem a tala, a fratura exposta ou a perna inutilizada): no passo da perna ruim, até
    /// 45% mais devagar, e a câmera desce 4 cm e inclina 1,5° para o lado dela. A mira tremendo pelo braço pior
    /// (<see cref="PartesDoCorpo.TremorDaMira"/>): um ruído lento (0,6 Hz) mais um tremor fino (7 Hz); mirando, a metade.
    /// Como a arma atira pela câmera, o tremor vale no tiro.
    /// </para>
    /// <para>
    /// A febre (a etapa 2: a febre e a sepse, sem o xarope agindo; <see cref="EstadoDoCorpo.ComFebre"/>), que entra e sai
    /// aos poucos: um Volume global só deste jogador (prioridade 50: a vinheta, a cor sem vida e um pouco quente, a
    /// exposição pulsando devagar e a visão turva de longe, um DepthOfField Gaussian), os calafrios em ondas (a cada 20 a
    /// 40 s, de 2 a 3 s de um tremor fino de 9 Hz, até 0,25° no olhar, 0,4° na sepse, sem giro de lado, que é o que mais
    /// enjoa) e os sons (<see cref="SomDoCorpo"/>: o calafrio e os dentes batendo com a onda, a respiração tremida; na sepse,
    /// a respiração pesada). Nada de distorção da lente, aberração cromática nem borrão de movimento. O Volume vale também
    /// na terceira pessoa e no carro, mas não com o jogador caído nem no modo vídeo. A opção "Menos balanço da câmera"
    /// (<see cref="Opcoes.CameraCalma"/>) deixa o mancar, o tremor do braço e o calafrio a 35% e tira a visão turva.
    /// </para>
    /// <para>
    /// A câmera: escreve a posição e a rotação locais da câmera do mundo (filha da cabeça, que o movimento gira; os braços
    /// de primeira pessoa são filhos dela e vão junto), em valores absolutos, só na primeira pessoa, a pé, fora da janela,
    /// vivo e sem tela aberta. Quem mais escreve nela: a terceira pessoa (atrás do ombro), o carro, o capô, a sinuca e a
    /// introdução do Dia 0. Por isso só volta ao zero se o que está na câmera ainda é o que ele pôs (senão, outro tomou
    /// conta dela e ele só esquece). O MovimentoPrimeiraPessoa.Tremer mexe na cabeça: os dois se somam.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(-50)]   // antes do movimento (que lê as velocidades no Update dele)
    public class EfeitosDoCorpo : MonoBehaviour
    {
        /// <summary>Os efeitos do jogador desta máquina.</summary>
        public static EfeitosDoCorpo Local { get; private set; }

        const float PeriodoDoPasso = 0.9f;     // um passo de cada perna (s), mancando
        const float LentoNoPasso = 0.45f;      // o quanto mais devagar no passo da perna ruim
        const float DescidaNoPasso = 0.04f;    // m
        const float InclinacaoNoPasso = 1.5f;  // graus
        const float Calma = 0.35f;             // "Menos balanço da câmera": o balanço que sobra
        // a fase 3: a câmera olhando para a parte tratada (o peso, e os ângulos da última parte, para sair suave)
        float _olhar, _olharPitch, _olharYaw;

        // a febre
        const float CalafrioNaFebre = 0.25f, CalafrioNaSepse = 0.4f;   // graus, no pico da onda
        const float HzDoCalafrio = 9f;
        const float VolumeDosSons = 0.35f;

        JogadorRede _rede;
        VidaJogador _vida;
        MovimentoPrimeiraPessoa _mov;
        PuloDaJanela _janela;
        PassageiroDoCarro _passageiro;
        TelaDoCapo _capo;
        CharacterController _cc;

        float _andar0, _correr0, _mirar0, _pulo0;
        bool _temOriginais;

        // o mancar: a fase do passo (anda só com o jogador andando) e o peso (entra e sai aos poucos)
        float _fase, _pesoDoMancar, _tremor, _sono;
        bool _forteE, _forteD;

        // a febre: o peso (0 a 1, entra e sai aos poucos), o quanto é sepse (0 a 1) e o quanto a tela mostra (0 caído, no vídeo)
        float _febre, _sepse, _mostrar;
        // as ondas do calafrio (Time.time): a próxima, a de agora (começo e duração) e o seu tremor (graus, já com o peso)
        float _proximoCalafrio = -1f, _calafrioDe = -10f, _calafrioDura = 2.5f, _calafrio;
        bool _dentesNaOnda;
        float _proximaRespiracao = -1f;

        // o Volume da febre (criado na primeira febre, só no dono)
        Volume _volume;
        VolumeProfile _perfil;
        Vignette _vinheta;
        ColorAdjustments _cor;
        DepthOfField _turva;
        AmbienteDoDia _ambiente;
        float _procurarAmbiente;

        // o que foi posto na câmera (para voltar ao zero só se ainda for o nosso)
        bool _mexi;
        Vector3 _posPosta;
        Quaternion _giroPosto = Quaternion.identity;

        void Awake()
        {
            _rede = GetComponent<JogadorRede>();
            _vida = GetComponent<VidaJogador>();
            _mov = GetComponent<MovimentoPrimeiraPessoa>();
            _janela = GetComponent<PuloDaJanela>();
            _passageiro = GetComponent<PassageiroDoCarro>();
            _capo = GetComponent<TelaDoCapo>();
            _cc = GetComponent<CharacterController>();
            Local = this;
            GuardarOriginais();
        }

        void GuardarOriginais()
        {
            if (_temOriginais || _mov == null) return;
            _andar0 = _mov.velocidadeAndando;
            _correr0 = _mov.velocidadeCorrendo;
            _mirar0 = _mov.velocidadeMirando;
            _pulo0 = _mov.alturaPulo;
            _temOriginais = true;
        }

        void OnDestroy()
        {
            if (Local == this) Local = null;
            // sem os efeitos, o movimento volta ao original
            if (_temOriginais && _mov != null)
            {
                _mov.velocidadeAndando = _andar0;
                _mov.velocidadeCorrendo = _correr0;
                _mov.velocidadeMirando = _mirar0;
                _mov.alturaPulo = _pulo0;
            }
            Soltar();
            // o Volume da febre e o perfil (com os componentes dele), feitos em código
            if (_volume != null) Destroy(_volume.gameObject);
            if (_perfil != null)
            {
                foreach (var c in _perfil.components)
                    if (c != null) Destroy(c);
                Destroy(_perfil);
            }
            _volume = null;
            _perfil = null;
        }

        // a perna manca forte: quebrada sem a tala, exposta ou inutilizada
        // (com a tala, o mancar forte some: anda com dificuldade, sem a câmera balançando)
        static bool MancaForte(in EstadoDoCorpo e, ParteDoCorpo perna) => PartesDoCorpo.MancaForte(e, perna);

        void Update()
        {
            if (_vida == null || _mov == null || !_vida.IsSpawned || !_vida.IsOwner) return;
            GuardarOriginais();
            float dt = Time.deltaTime;
            var e = _vida.Corpo.Value;

            // o mancar: a fase anda com o jogador andando no chão
            _forteE = MancaForte(e, ParteDoCorpo.PernaE);
            _forteD = MancaForte(e, ParteDoCorpo.PernaD);
            Vector3 vel = _cc != null && _cc.enabled ? _cc.velocity : Vector3.zero;
            float andando = new Vector2(vel.x, vel.z).magnitude;
            bool mancando = (_forteE || _forteD) && andando > 0.15f && _cc != null && _cc.isGrounded && !_vida.Caido.Value;
            _pesoDoMancar = Mathf.MoveTowards(_pesoDoMancar, mancando ? 1f : 0f, dt * 4f);
            if (mancando) _fase = Mathf.Repeat(_fase + dt / PeriodoDoPasso, 1f);
            float lento = 1f - LentoNoPasso * NoPassoRuim();

            float fator = PartesDoCorpo.FatorDeAndar(e);
            _mov.velocidadeAndando = _andar0 * fator * lento;
            _mov.velocidadeMirando = _mirar0 * fator * lento;
            _mov.velocidadeCorrendo = PartesDoCorpo.SemCorrer(e) ? _mov.velocidadeAndando : _correr0 * fator * lento;
            _mov.alturaPulo = PartesDoCorpo.PuloQueSobra(e, _pulo0);

            // o tremor da mira entra e sai aos poucos
            bool mirando = _rede != null && _rede.Mirando.Value;
            // (06/11/2026, a mente: a mão da mente abalada e da crise de pânico também treme; vale o maior)
            float tremor = Mathf.Max(PartesDoCorpo.TremorDaMira(e), CriseDePanico.TremorAgora);
            // as habilidades (06/11/2026): a Mira de aço não treme; a Mão firme treme menos
            if (Habilidades.Tem(Habilidade.MiraDeAco)) tremor = 0f;
            else if (Habilidades.Tem(Habilidade.MaoFirme)) tremor *= 0.75f;
            if (mirando) tremor *= 0.5f;
            _tremor = Mathf.MoveTowards(_tremor, tremor, dt * 2f);

            Febre(e, dt, mirando);
        }

        // ------------------------------------------------ a febre ------------------------------------------------

        // o peso da febre, as ondas do calafrio, os sons e o Volume
        void Febre(in EstadoDoCorpo e, float dt, bool mirando)
        {
            bool comFebre = e.ComFebre;
            // entra em uns 8 s; sai em uns 5 (o xarope agindo, o antibiótico curando)
            _febre = Mathf.MoveTowards(_febre, comFebre ? 1f : 0f, dt * (comFebre ? 0.125f : 0.2f));
            _sepse = Mathf.MoveTowards(_sepse, comFebre && e.Sepse ? 1f : 0f, dt * 0.15f);
            // caído, a febre zera (o renascer sara tudo: a vida nova começa sem a tela turva)
            if (_vida.Caido.Value) { _febre = 0f; _sepse = 0f; }
            bool podeMostrar = !_vida.Caido.Value && !ModoVideo.Ligado;
            _mostrar = Mathf.MoveTowards(_mostrar, podeMostrar ? 1f : 0f, dt * 3f);
            float t = Time.time;

            // as ondas do calafrio: a cada 20 a 40 s (a primeira logo, para ele perceber), de 2 a 3 s
            bool sons = podeMostrar && !MenuOnline.Cinema;
            if (comFebre && podeMostrar)
            {
                if (_proximoCalafrio < 0f) _proximoCalafrio = t + Random.Range(6f, 14f);
                if (t >= _proximoCalafrio)
                {
                    _calafrioDe = t;
                    _calafrioDura = Random.Range(2f, 3f);
                    _proximoCalafrio = t + _calafrioDura + Random.Range(20f, 40f);
                    _dentesNaOnda = false;
                    if (sons) Tocar(SomDoCorpo.Calafrio, VolumeDosSons * Mathf.Max(0.4f, _febre));
                }
            }
            else if (!comFebre) _proximoCalafrio = -1f;

            float k = (t - _calafrioDe) / _calafrioDura;
            float onda = k >= 0f && k <= 1f ? Mathf.Clamp01(k / 0.15f) * Mathf.Clamp01((1f - k) / 0.35f) : 0f;
            // os dentes batem no meio da onda
            if (onda > 0f && k >= 0.3f && !_dentesNaOnda)
            {
                _dentesNaOnda = true;
                if (sons && Random.value < 0.7f) Tocar(SomDoCorpo.Dentes, VolumeDosSons * Mathf.Max(0.4f, _febre));
            }
            float pico = Mathf.Lerp(CalafrioNaFebre, CalafrioNaSepse, _sepse);
            _calafrio = pico * onda * _febre * _mostrar * (mirando ? 0.5f : 1f) * (Opcoes.CameraCalma ? Calma : 1f);

            // a respiração: tremida a cada 6 a 10 s na febre; pesada e mais seguida na sepse
            if (comFebre && sons && _febre > 0.3f)
            {
                if (_proximaRespiracao < 0f) _proximaRespiracao = t + Random.Range(2f, 5f);
                if (t >= _proximaRespiracao)
                {
                    bool pesada = e.Sepse;
                    Tocar(pesada ? SomDoCorpo.RespiracaoPesada : SomDoCorpo.Respiracao, VolumeDosSons * _febre * (pesada ? 1.1f : 0.85f));
                    _proximaRespiracao = t + (pesada ? Random.Range(3f, 4.5f) : Random.Range(6f, 10f));
                }
            }
            else if (!comFebre) _proximaRespiracao = -1f;

            // (06/11/2026, a mente: dois calmantes seguidos, sonolento: a vista embaça um pouco, bem pouco)
            _sono = Mathf.MoveTowards(_sono, _vida.TemEfeito(VidaJogador.EfeitoSonolento) && !_vida.Caido.Value ? 1f : 0f, dt * 0.4f);
            AtualizarVolume(_febre * _mostrar, t, _sono * _mostrar);
        }

        void Tocar(AudioClip clip, float volume)
        {
            if (clip == null || _rede == null || _rede.cameraMundo == null) return;
            Efeitos.Som(clip, _rede.cameraMundo.transform.position, volume, false);
        }

        // o Volume da febre: cada valor é o da tela de agora (o dia e a noite) misturado com o da febre pelo peso; por isso o
        // Volume fica com o peso 1 e é desligado sem a febre. (O DepthOfField não se mistura pelo peso: o modo liga de uma
        // vez e o raio não desce de 0,5. A visão turva entra trazendo o começo do borrão de longe, na neblina, para perto.)
        void AtualizarVolume(float f, float t, float sono)
        {
            if (f <= 0.001f && sono <= 0.001f)
            {
                if (_volume != null && _volume.enabled) _volume.enabled = false;
                return;
            }
            if (_volume == null) CriarVolume();
            if (!_volume.enabled) _volume.enabled = true;
            _volume.weight = 1f;

            LerATela(out float exposicao, out float saturacao, out Color filtro, out float vinheta, out float suave);
            // a vinheta: 0,3 (0,4 na sepse), e sempre um pouco acima da do dia (que já é 0,3)
            float vinhetaDaFebre = Mathf.Max(Mathf.Lerp(0.3f, 0.4f, _sepse), vinheta + Mathf.Lerp(0.08f, 0.14f, _sepse));
            _vinheta.intensity.Override(Mathf.Clamp01(Mathf.Lerp(vinheta, vinhetaDaFebre, f)));
            _vinheta.smoothness.Override(Mathf.Clamp(Mathf.Lerp(suave, 0.5f, f), 0.01f, 1f));
            // a cor: 15 de saturação a menos, um filtro um pouco quente e a exposição pulsando devagar (0,1 Hz)
            float pulso = Mathf.Sin(t * 0.1f * 2f * Mathf.PI) * 0.15f;
            _cor.postExposure.Override(Mathf.Lerp(exposicao, exposicao + pulso, f));
            _cor.saturation.Override(Mathf.Clamp(Mathf.Lerp(saturacao, saturacao - 15f, f), -100f, 100f));
            var quente = new Color(filtro.r, filtro.g * 0.95f, filtro.b * 0.86f, filtro.a);
            _cor.colorFilter.Override(Color.Lerp(filtro, quente, f));
            // a visão turva: o borrão começa a 6 m (4 m na sepse) e fecha aos 25 m; sem a febre, lá longe na neblina
            bool turva = !Opcoes.CameraCalma;
            _turva.active = turva;
            if (turva)
            {
                float comeco = Mathf.Lerp(6f, 4f, _sepse);
                // (sonolento: o borrão começa a 14 m e fecha aos 45; vale o mais perto dos dois)
                _turva.gaussianStart.Override(Mathf.Min(Mathf.Lerp(80f, comeco, f), Mathf.Lerp(80f, 14f, sono)));
                _turva.gaussianEnd.Override(Mathf.Min(Mathf.Lerp(120f, 25f, f), Mathf.Lerp(120f, 45f, sono)));
            }
        }

        void CriarVolume()
        {
            _perfil = ScriptableObject.CreateInstance<VolumeProfile>();
            _perfil.name = "Febre";
            _vinheta = _perfil.Add<Vignette>();
            _vinheta.color.Override(Color.black);
            _vinheta.intensity.Override(0f);
            _vinheta.smoothness.Override(0.5f);
            _cor = _perfil.Add<ColorAdjustments>();
            _cor.postExposure.Override(0f);
            _cor.saturation.Override(0f);
            _cor.colorFilter.Override(Color.white);
            // Na build, o shader do desfoque só vai porque o perfil Build_EfeitosCriadosNoJogo (em _Game/Settings) tem um
            // DepthOfField Gaussian ligado: a URP corta os efeitos que nenhum perfil salvo no projeto usa.
            _turva = _perfil.Add<DepthOfField>();
            _turva.mode.Override(DepthOfFieldMode.Gaussian);
            _turva.gaussianStart.Override(80f);
            _turva.gaussianEnd.Override(120f);
            _turva.gaussianMaxRadius.Override(0.7f);
            _turva.highQualitySampling.Override(false);
            _volume = new GameObject("Febre").AddComponent<Volume>();
            _volume.transform.SetParent(transform, false);
            _volume.isGlobal = true;
            _volume.priority = 50f;
            _volume.weight = 0f;
            _volume.sharedProfile = _perfil;
        }

        // a tela de agora, sem a febre: os dois Volumes do jogo (a noite e o dia, misturados pelo AmbienteDoDia), na ordem
        // da prioridade, com os padrões da URP por baixo
        void LerATela(out float exposicao, out float saturacao, out Color filtro, out float vinheta, out float suave)
        {
            exposicao = 0f;
            saturacao = 0f;
            filtro = Color.white;
            vinheta = 0f;
            suave = 0.2f;
            if (_ambiente == null && Time.time >= _procurarAmbiente)
            {
                _procurarAmbiente = Time.time + 2f;
                _ambiente = FindAnyObjectByType<AmbienteDoDia>();
            }
            if (_ambiente == null) return;
            Volume a = _ambiente.volumeNoite, b = _ambiente.volumeDia;
            if (a != null && b != null && b.priority < a.priority) (a, b) = (b, a);
            Misturar(a, ref exposicao, ref saturacao, ref filtro, ref vinheta, ref suave);
            Misturar(b, ref exposicao, ref saturacao, ref filtro, ref vinheta, ref suave);
        }

        static void Misturar(Volume v, ref float exposicao, ref float saturacao, ref Color filtro, ref float vinheta, ref float suave)
        {
            if (v == null || !v.isActiveAndEnabled || v.sharedProfile == null) return;
            float w = Mathf.Clamp01(v.weight);
            if (w <= 0f) return;
            if (v.sharedProfile.TryGet(out ColorAdjustments c) && c.active)
            {
                if (c.postExposure.overrideState) exposicao = Mathf.Lerp(exposicao, c.postExposure.value, w);
                if (c.saturation.overrideState) saturacao = Mathf.Lerp(saturacao, c.saturation.value, w);
                if (c.colorFilter.overrideState) filtro = Color.Lerp(filtro, c.colorFilter.value, w);
            }
            if (v.sharedProfile.TryGet(out Vignette g) && g.active)
            {
                if (g.intensity.overrideState) vinheta = Mathf.Lerp(vinheta, g.intensity.value, w);
                if (g.smoothness.overrideState) suave = Mathf.Lerp(suave, g.smoothness.value, w);
            }
        }

        // ------------------------------------------------ a câmera ------------------------------------------------

        // o quanto o passo de agora é o da perna ruim (0 a 1): a primeira metade da fase é a perna esquerda, a segunda a direita
        float NoPassoRuim()
        {
            float s = Mathf.Sin(_fase * 2f * Mathf.PI);
            float q = s > 0f ? (_forteE ? s : 0f) : (_forteD ? -s : 0f);
            return q * _pesoDoMancar;
        }

        void LateUpdate()
        {
            if (_vida == null || _mov == null || _rede == null || _rede.cameraMundo == null || !_vida.IsSpawned || !_vida.IsOwner) return;
            var cam = _rede.cameraMundo.transform;
            // o desvio da mira (o tremor do braço e o calafrio), também na terceira pessoa: lá a câmera não treme, e o
            // ArmaJogador gira o tiro por ele (DesvioDaMira)
            DesvioDaMira = Quaternion.Euler(TremorAgora(out float p0, out float y0) ? p0 : 0f, y0, 0f);
            bool pode = !_rede.EmTerceiraPessoa
                        && !(_passageiro != null && _passageiro.NoCarro)
                        && !(_janela != null && _janela.Pulando)
                        && !(_capo != null && _capo.Aberta)
                        && !_vida.Caido.Value && _mov.enabled
                        && !MenuOnline.Cinema && !MenuOnline.ComTela && !ModoVideo.Ligado;
            if (!pode)
            {
                Soltar();
                return;
            }
            // "Menos balanço da câmera": o mancar e o tremor do braço a 35% (o calafrio já vem com ela)
            float calma = Opcoes.CameraCalma ? Calma : 1f;

            // o passo da perna ruim: desce e inclina para o lado dela
            float s = Mathf.Sin(_fase * 2f * Mathf.PI);
            float q = NoPassoRuim() * calma;
            float lado = s > 0f ? 1f : -1f;   // a perna esquerda: a cabeça cai para a esquerda (o giro em z positivo)
            Vector3 pos = new Vector3(0f, -DescidaNoPasso * q, 0f);
            float rolar = InclinacaoNoPasso * q * lado;

            // a mira tremendo: o ruído lento mais o tremor fino
            float pitch = 0f, yaw = 0f;
            float tremor = _tremor * calma;
            float t = Time.time;
            if (tremor > 0.001f)
            {
                float lentoX = (Mathf.PerlinNoise(t * 0.6f, 3.7f) - 0.5f) * 2f;
                float lentoY = (Mathf.PerlinNoise(11.3f, t * 0.6f) - 0.5f) * 2f;
                float finoX = (Mathf.PerlinNoise(t * 7f, 23.1f) - 0.5f) * 2f;
                float finoY = (Mathf.PerlinNoise(41.9f, t * 7f) - 0.5f) * 2f;
                pitch = tremor * (lentoX + finoX * 0.25f);
                yaw = tremor * (lentoY + finoY * 0.25f);
            }

            // o calafrio da febre: um tremor fino de 9 Hz, só no olhar (em cima e embaixo, e dos lados), sem giro de lado
            if (_calafrio > 0.0001f)
            {
                float w = 2f * Mathf.PI * HzDoCalafrio * t;
                float cx = Mathf.Clamp(Mathf.Sin(w) * 0.6f + (Mathf.PerlinNoise(t * HzDoCalafrio, 57.3f) - 0.5f) * 0.8f, -1f, 1f);
                float cy = Mathf.Clamp(Mathf.Sin(w * 1.07f + 1.3f) * 0.6f + (Mathf.PerlinNoise(71.9f, t * HzDoCalafrio) - 0.5f) * 0.8f, -1f, 1f);
                pitch += _calafrio * cx;
                yaw += _calafrio * cy;
            }

            // a fase 3: a câmera olha para a parte que está sendo tratada (a perna lá embaixo, o braço do lado, o amigo à frente)
            var tr = Tratamento.Local;
            bool tratando = tr != null && tr.Ativo;
            if (tratando)
            {
                var p = tr.Parte;
                bool boca = PartesDoCorpo.PelaBoca(tr.Item);
                _olharPitch = tr.Alvo != null ? 18f : boca ? -4f
                            : p == ParteDoCorpo.PernaE || p == ParteDoCorpo.PernaD ? 48f
                            : p == ParteDoCorpo.BracoE || p == ParteDoCorpo.BracoD ? 24f
                            : p == ParteDoCorpo.Tronco ? 32f : 0f;
                _olharYaw = tr.Alvo != null || boca ? 0f
                          : p == ParteDoCorpo.BracoE ? -16f : p == ParteDoCorpo.BracoD ? 16f
                          : p == ParteDoCorpo.PernaE ? -6f : p == ParteDoCorpo.PernaD ? 6f : 0f;
            }
            _olhar = Mathf.MoveTowards(_olhar, tratando ? 1f : 0f, Time.deltaTime * 3f);
            float olhar = Mathf.SmoothStep(0f, 1f, _olhar);
            // a cabeça já está girada pelo olhar do jogador (até 85°): a soma não passa de 82° para baixo nem para cima
            float jaOlha = _rede != null && _rede.IsSpawned ? _rede.Pitch.Value : 0f;
            pitch += Mathf.Clamp(_olharPitch * olhar, -82f - jaOlha, 82f - jaOlha);
            yaw += _olharYaw * olhar;

            bool nada = q <= 0.0001f && tremor <= 0.001f && _calafrio <= 0.0001f && _olhar <= 0.0001f;
            if (nada)
            {
                Soltar();
                return;
            }
            // valores absolutos (nada se acumula de um quadro para o outro)
            var giro = Quaternion.Euler(pitch, yaw, rolar);
            cam.localPosition = pos;
            cam.localRotation = giro;
            _posPosta = pos;
            _giroPosto = giro;
            _mexi = true;
        }

        /// <summary>
        /// O giro da mira de agora (o tremor do braço e o calafrio, sem o mancar). Na terceira pessoa, o tiro sai da mira do
        /// centro da tela, que não treme: o ArmaJogador gira a direção por este desvio.
        /// </summary>
        public Quaternion DesvioDaMira { get; private set; } = Quaternion.identity;

        // o tremor do braço e o calafrio deste quadro (os mesmos números que vão para a câmera na primeira pessoa)
        bool TremorAgora(out float pitch, out float yaw)
        {
            pitch = yaw = 0f;
            if (_vida == null || _vida.Caido.Value) return false;
            float calma = Opcoes.CameraCalma ? Calma : 1f;
            float tremor = _tremor * calma;
            float t = Time.time;
            if (tremor > 0.001f)
            {
                pitch = tremor * ((Mathf.PerlinNoise(t * 0.6f, 3.7f) - 0.5f) * 2f + (Mathf.PerlinNoise(t * 7f, 23.1f) - 0.5f) * 0.5f);
                yaw = tremor * ((Mathf.PerlinNoise(11.3f, t * 0.6f) - 0.5f) * 2f + (Mathf.PerlinNoise(41.9f, t * 7f) - 0.5f) * 0.5f);
            }
            if (_calafrio > 0.0001f)
            {
                float w = 2f * Mathf.PI * HzDoCalafrio * t;
                pitch += _calafrio * Mathf.Clamp(Mathf.Sin(w) * 0.6f + (Mathf.PerlinNoise(t * HzDoCalafrio, 57.3f) - 0.5f) * 0.8f, -1f, 1f);
                yaw += _calafrio * Mathf.Clamp(Mathf.Sin(w * 1.07f + 1.3f) * 0.6f + (Mathf.PerlinNoise(71.9f, t * HzDoCalafrio) - 0.5f) * 0.8f, -1f, 1f);
            }
            return true;
        }

        // o efeito acabou (ou outro vai cuidar da câmera): volta ao zero, se o que está lá ainda é o nosso
        void Soltar()
        {
            if (!_mexi) return;
            _mexi = false;
            if (_rede == null || _rede.cameraMundo == null) return;
            var cam = _rede.cameraMundo.transform;
            if ((cam.localPosition - _posPosta).sqrMagnitude < 1e-6f && Quaternion.Angle(cam.localRotation, _giroPosto) < 0.01f)
            {
                cam.localPosition = Vector3.zero;
                cam.localRotation = Quaternion.identity;
            }
        }
    }
}
