using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.UI;
using ZombieCityCleaner.Video;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// A crise de pânico e o corpo da mente abalada (06/11/2026, a sanidade, a etapa 2: "estilo crise de pânico nos
    /// jogadores, onde ficam ofegantes (mudaria o som, respiração, palpitações um pouco alta, trilha sonora mais agitada e
    /// ansiosa)"), só no dono.
    /// <para>Abaixo de 70, a mão treme um pouco (a mira, pelo <see cref="EfeitosDoCorpo"/>); abaixo de 40, o coração se ouve
    /// baixinho a cada batida do <see cref="MonitorCardiaco"/>, uma respiração funda de vez em quando, e as bordas da tela
    /// escurecem no ritmo dele.</para>
    /// <para>A crise: um susto (a mente caindo 3,5 ou mais de uma vez) com a mente abaixo de 40, ou sozinha abaixo de 25 (de
    /// 30 em 30 s, com chance maior quanto mais baixa). Ela dura de 20 a 35 s: o coração dispara (+55 bpm no monitor), a
    /// palpitação fica alta, a respiração ofegante, a mira treme forte e a tela pulsa em vermelho escuro. A
    /// <see cref="Intensidade"/> (0 a 1) é para a trilha sonora (a sessão da música liga depois).</para>
    /// </summary>
    public class CriseDePanico : MonoBehaviour
    {
        /// <summary>O do jogador desta máquina.</summary>
        public static CriseDePanico Local { get; private set; }
        /// <summary>A crise agora (0 a 1, sobe e desce aos poucos): a trilha ansiosa lê daqui.</summary>
        public static float Intensidade => Local != null ? Local._crise : 0f;
        /// <summary>O tremor da mira da mente (graus): o <see cref="EfeitosDoCorpo"/> soma ao do braço.</summary>
        public static float TremorAgora { get; private set; }

        /// <summary>Em crise agora.</summary>
        public bool Ativa => Time.time < _criseAte;

        VidaJogador _vida;
        JogadorRede _rede;
        float _criseAte, _crise, _ultimaMente = -1f, _proximaAleatoria, _proximaRespiracao, _batidaTocada;

        void Awake()
        {
            _vida = GetComponent<VidaJogador>();
            _rede = GetComponent<JogadorRede>();
        }

        void OnEnable() => Local = this;

        void OnDisable()
        {
            if (Local == this) Local = null;
            TremorAgora = 0f;
        }

        /// <summary>Começa (ou estica) a crise por estes segundos (o F1 também).</summary>
        public void Comecar(float segundos)
        {
            _criseAte = Mathf.Max(_criseAte, Time.time + segundos);
        }

        /// <summary>A crise passa (o calmante, na etapa 3).</summary>
        public void Acalmar() => _criseAte = 0f;

        bool PodeSentir => _vida != null && _vida.IsSpawned && !_vida.Caido.Value && !_vida.Desmaiado.Value && !_vida.Dormindo.Value && VidaJogador.CorpoAnda;

        Vector3 Ouvido => _rede != null && _rede.cameraMundo != null ? _rede.cameraMundo.transform.position : transform.position + Vector3.up * 1.6f;

        void Update()
        {
            if (_vida == null || !_vida.IsSpawned) return;
            float dt = Time.deltaTime;
            float mente = _vida.Sanidade.Value;
            bool sente = PodeSentir;
            // o susto: a mente caindo de uma vez
            if (_ultimaMente >= 0f && sente && _ultimaMente - mente >= 3.5f && mente < 40f) Comecar(Random.Range(20f, 35f));
            _ultimaMente = mente;
            // sozinha, abaixo de 25
            if (sente && mente < 25f && Time.time >= _proximaAleatoria)
            {
                _proximaAleatoria = Time.time + 30f;
                if (Random.value < (25f - mente) / 25f * 0.45f) Comecar(Random.Range(20f, 30f));
            }
            // o calmante agindo (a etapa 3): a crise passa e não volta enquanto ele age
            if (!sente || _vida.MenteTem(MotivoDaMente.Calmante)) _criseAte = 0f;
            _crise = Mathf.MoveTowards(_crise, Ativa ? 1f : 0f, dt * (Ativa ? 0.5f : 0.12f));

            var monitor = MonitorCardiaco.Local;
            if (monitor != null) monitor.Extra = 55f * _crise;
            float abalo = Mathf.Clamp01((70f - mente) / 70f);
            // (a etapa 3: a abstinência da nicotina também treme a mão)
            float abstinencia = _vida.TemEfeito(VidaJogador.EfeitoAbstinenciaForte) ? 0.3f : _vida.TemEfeito(VidaJogador.EfeitoAbstinencia) ? 0.15f : 0f;
            TremorAgora = sente ? 0.12f * abalo + 0.55f * _crise + abstinencia : 0f;
            if (!sente) return;

            // a palpitação: o "tum-tum" em cada batida do monitor (abaixo de 40, baixinho; na crise, alto)
            float baixo = mente < 40f ? 0.06f + 0.12f * (40f - mente) / 40f : 0f;
            float volume = Mathf.Max(baixo, 0.55f * _crise);
            if (monitor != null && monitor.UltimaBatida > _batidaTocada)
            {
                _batidaTocada = monitor.UltimaBatida;
                if (volume > 0.02f) Efeitos.Som(SomDaMente.Coracao, Ouvido, volume, false);
            }
            // a respiração: na crise, ofegante (curta e seguida); abaixo de 40, uma funda de vez em quando
            if (Time.time >= _proximaRespiracao && (_crise > 0.3f || mente < 40f))
            {
                if (_crise > 0.3f)
                {
                    Efeitos.Som(SomDoCorpo.RespiracaoPesada, Ouvido, 0.25f + 0.35f * _crise, false);
                    _proximaRespiracao = Time.time + Random.Range(0.9f, 1.3f);
                }
                else
                {
                    Efeitos.Som(SomDoCorpo.Respiracao, Ouvido, 0.25f, false);
                    _proximaRespiracao = Time.time + Random.Range(7f, 14f);
                }
            }
        }

        void OnGUI()
        {
            if (!PodeSentir || Event.current.type != EventType.Repaint) return;
            if (MenuOnline.Cinema || ModoVideo.Ligado) return;
            float mente = _vida.Sanidade.Value;
            float baixo = mente < 40f ? 0.12f * (40f - mente) / 40f : 0f;
            float forca = Mathf.Max(baixo, 0.5f * _crise);
            if (forca < 0.01f) return;
            // as bordas escurecem no ritmo do coração (a batida acende e some)
            var monitor = MonitorCardiaco.Local;
            float batida = monitor != null ? Mathf.Exp(-(Time.time - monitor.UltimaBatida) * 6f) : 0f;
            var antes = GUI.color;
            GUI.color = new Color(0.32f, 0f, 0.04f, forca * (0.55f + 0.45f * batida));
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), IconesDoCorpo.Vinheta);
            GUI.color = antes;
        }
    }
}
