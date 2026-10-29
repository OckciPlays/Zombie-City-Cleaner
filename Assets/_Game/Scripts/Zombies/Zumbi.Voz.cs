using UnityEngine;

namespace ZombieCityCleaner.Zombies
{
    /// <summary>
    /// A voz do Oco e a agonia no sol (a etapa 7, a pedido em 28/10/2026: "um som específico para o Oco, aterrorizante, mas
    /// sem deixar de ser um zumbi"; o som padrão, o do golpe, o chamado entre eles, a agonia no sol, a dor do tiro, a pedra;
    /// e a animação de agonia). Tudo aqui roda em todas as máquinas (no anfitrião e nas cópias): é só som e animação.
    /// <para><b>A voz:</b> uma fala por vez, na fonte "voz" do zumbi; uma mais forte corta uma mais fraca (a agonia &gt; a
    /// dor &gt; o golpe &gt; o chamado &gt; a resposta &gt; o gemido), nunca o contrário. O gemido, o chamado e a agonia
    /// passam pelo limite de vozes novas por segundo (<see cref="ZombieManager.PodeGemer"/>); o golpe e a dor, só pela
    /// distância.</para>
    /// <para><b>O chamado:</b> quem começa a perseguir chama (e, de vez em quando, um parado chama à toa); até 2 dos de perto
    /// (16 m) respondem um pouco depois. No anfitrião, quem responde parado vira para quem chamou.</para>
    /// <para><b>A agonia:</b> enquanto a pedra sobe (ele está no sol), o tronco faz a agonia do jeito dele
    /// (<see cref="TipoDeAgonia"/>, pelo Id: as mãos no rosto, limpando a pele, encolhido, ou como antes, só se
    /// debatendo), ele grita de tempos em tempos e a crosta estala (perto de quem escuta). Virando estátua, a pose fica: a
    /// estátua guarda a agonia. A estátua que já nasce de pedra também ganha a pose.</para>
    /// </summary>
    public partial class Zumbi
    {
        enum Fala : byte { Nada, Voz, Resposta, Chamado, Golpe, Tiro, Agonia }

        static readonly int AgoniaId = Animator.StringToHash("Agonia");
        static readonly string[] EstadosDaAgonia = { null, "AgoniaRosto", "AgoniaPele", "AgoniaEncolher" };
        const int CamadaDasAcoes = 1;

        Fala _falaAtual;
        float _falaAte, _volumeDaVoz = -1f, _responderEm = -1f, _pedraAntes, _pedraSubiuEm = -100f, _proximoGritoDeAgonia;
        int _temAgonia = -1;   // o Animator tem o parâmetro "Agonia"? (-1: ainda não sabe)
        AudioSource _somDaPedra;

        /// <summary>
        /// O jeito de agonizar no sol, pelo Id e a geração (igual em todas as máquinas): 0 como antes (só se debate), 1 as
        /// mãos no rosto, 2 limpando a pele, 3 encolhido.
        /// </summary>
        internal int TipoDeAgonia => (Id * 5 + Geracao * 3 + 2) % 4;

        SonsDoOco Sons => _m != null ? _m.Sons : null;

        bool TemAgonia()
        {
            if (_temAgonia < 0 && animador != null && animador.isActiveAndEnabled && animador.runtimeAnimatorController != null)
            {
                _temAgonia = 0;
                foreach (var p in animador.parameters)
                    if (p.nameHash == AgoniaId) _temAgonia = 1;
            }
            return _temAgonia == 1;
        }

        /// <summary>Toca uma fala (um clipe sorteado), se nenhuma mais forte está tocando.</summary>
        bool Falar(AudioClip[] clips, Fala tipo, float volume, float tom = 1f, bool comLimite = true)
        {
            if (clips == null || clips.Length == 0 || voz == null || _m == null || Morto || Petrificado) return false;
            if (Pedra >= 0.95f && tipo != Fala.Agonia) return false;
            if (voz.isPlaying && Time.time < _falaAte && tipo <= _falaAtual) return false;
            if (comLimite ? !_m.PodeGemer(transform.position) : !_m.PodeOuvir(transform.position)) return false;
            if (_volumeDaVoz < 0f) _volumeDaVoz = voz.volume;
            var clip = clips[Random.Range(0, clips.Length)];
            if (clip == null) return false;
            voz.Stop();
            voz.clip = clip;
            voz.pitch = tom * Random.Range(0.93f, 1.06f);
            voz.volume = _volumeDaVoz * volume;
            voz.time = 0f;
            voz.Play();
            float duracao = clip.length / voz.pitch;
            voz.SetScheduledEndTime(AudioSettings.dspTime + duracao + 0.05);   // desfaz o fim marcado de um trecho antigo
            _falaAtual = tipo;
            _falaAte = Time.time + duracao;
            return true;
        }

        /// <summary>O chamado (achou alguém, ou só chamando): os de perto respondem.</summary>
        internal void Chamar()
        {
            var s = Sons;
            if (s == null || s.chamado == null || s.chamado.Length == 0)
            {
                Gemer(1.05f);
                return;
            }
            if (Falar(s.chamado, Fala.Chamado, 1f)) _m.Responder(this);
        }

        /// <summary>Ouviu um chamado: responde daqui a pouco; no anfitrião, parado, vira para quem chamou.</summary>
        internal void OuvirChamado(Vector3 de, float quando)
        {
            if (Morto || Petrificado) return;
            if (_responderEm < 0f) _responderEm = quando;
            if (!Copia && Atual == Estado.Parado) OlharPara(de);
        }

        /// <summary>A cada quadro (níveis Perto e Médio, em todas as máquinas): a resposta marcada e a agonia no sol.</summary>
        void VozNoQuadro()
        {
            if (_responderEm > 0f && Time.time >= _responderEm)
            {
                _responderEm = -1f;
                var s = Sons;
                if (s != null) Falar(s.resposta, Fala.Resposta, 0.85f);
            }
            Agonizar();
        }

        // enquanto a pedra sobe (no sol): o tronco agoniza, ele grita de tempos em tempos e a crosta estala
        void Agonizar()
        {
            if (Pedra > _pedraAntes + 1e-5f) _pedraSubiuEm = Time.time;
            _pedraAntes = Pedra;
            // a cópia recebe a pedra de 3 a 10 vezes por segundo: espera um pouco mais antes de achar que parou de subir
            bool agonizando = !Morto && !Petrificado && Pedra > 0.001f && Time.time - _pedraSubiuEm < (Copia ? 0.8f : 0.4f);
            if (animador.isActiveAndEnabled && TemAgonia()) animador.SetInteger(AgoniaId, agonizando ? TipoDeAgonia : 0);
            var s = Sons;
            if (s == null) return;
            if (agonizando && Time.time >= _proximoGritoDeAgonia)
                _proximoGritoDeAgonia = Falar(s.agonia, Fala.Agonia, 1f, Random.Range(0.95f, 1.08f)) ? _falaAte + Random.Range(0.15f, 0.9f) : Time.time + 0.4f;
            CrostaEstalando(agonizando, s);
        }

        // a crosta estalando, em laço, numa segunda fonte (só perto de quem escuta: 18 m), mais forte quanto mais pedra
        void CrostaEstalando(bool agonizando, SonsDoOco s)
        {
            if (agonizando && s.petrificando != null && _m.PodeOuvir(transform.position, 18f))
            {
                if (_somDaPedra == null) _somDaPedra = CriarSomDaPedra();
                if (!_somDaPedra.isPlaying)
                {
                    _somDaPedra.clip = s.petrificando;
                    _somDaPedra.time = Random.Range(0f, s.petrificando.length * 0.9f);
                    _somDaPedra.Play();
                }
                _somDaPedra.volume = Mathf.Lerp(0.3f, 0.75f, Pedra);
            }
            else if (_somDaPedra != null && _somDaPedra.isPlaying) _somDaPedra.Stop();
        }

        AudioSource CriarSomDaPedra()
        {
            var a = gameObject.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.loop = true;
            a.dopplerLevel = 0f;
            a.priority = 200;   // dos menos importantes: com muitos, a Unity corta estes primeiro
            if (voz != null)
            {
                a.spatialBlend = voz.spatialBlend;
                a.rolloffMode = voz.rolloffMode;
                a.minDistance = voz.minDistance;
                a.maxDistance = voz.maxDistance;
                a.spread = voz.spread;
                a.outputAudioMixerGroup = voz.outputAudioMixerGroup;
                if (voz.rolloffMode == AudioRolloffMode.Custom)
                    a.SetCustomCurve(AudioSourceCurveType.CustomRolloff, voz.GetCustomCurve(AudioSourceCurveType.CustomRolloff));
            }
            else
            {
                a.spatialBlend = 1f;
                a.maxDistance = 25f;
            }
            return a;
        }

        /// <summary>A estátua que já nasce de pedra (ou que ninguém viu subir): o tronco numa pose da agonia, se ele tem uma.</summary>
        void PosarAAgonia()
        {
            int tipo = TipoDeAgonia;
            if (tipo == 0 || !TemAgonia()) return;
            animador.SetInteger(AgoniaId, tipo);
            animador.Play(EstadosDaAgonia[tipo], CamadaDasAcoes, Random.value);
        }

        // o som da reação (no anfitrião e nas cópias): a dor do tiro, ou o susto da luz; no sol, a agonia já grita
        void SomDaReacao()
        {
            var s = Sons;
            if (s == null || Petrificado || Time.time - _soltouEm < 0.3f) return;
            if (Atual == Estado.Esquivando) Falar(s.tiro, Fala.Tiro, 0.7f, 1.12f, false);
            else if (Atual != Estado.Fugindo) Falar(s.tiro, Fala.Tiro, 1f, 1f, false);
        }

        void ZerarVoz()
        {
            _falaAtual = Fala.Nada;
            _falaAte = 0f;
            _responderEm = -1f;
            _pedraAntes = 0f;
            _pedraSubiuEm = -100f;
            _proximoGritoDeAgonia = 0f;
            if (_somDaPedra != null) _somDaPedra.Stop();
        }
    }
}
