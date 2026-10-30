using System.Collections.Generic;
using UnityEngine;
using ZombieCityCleaner.Combat;
using ZombieCityCleaner.World;
using Random = UnityEngine.Random;

namespace ZombieCityCleaner.Music
{
    /// <summary>
    /// Os sons de ambiente (a pedido em 29/10/2026: "isso deve entrar de uma maneira que a trilha sonora e esses sons
    /// ambientes se encaixem"). O mesmo maestro decide a música e o ambiente, então um respeita o outro:
    /// <list type="bullet">
    /// <item>No silêncio (só o vento), o ambiente aparece mais (um som a cada 9 a 20 s); com a música da exploração, raro e
    /// mais baixo (a cada 25 a 50 s); no perigo e nos momentos (o "te viram", a saída, o amanhecer, a morte), nada além do
    /// vento.</item>
    /// <item>O que o vento mexe (a janela batendo, a porta, a placa de metal rangendo, a lata rolando) acontece logo depois
    /// de uma rajada do vento da trilha passar (os tempos das rajadas vêm do Ferramentas/trilha, no trilha.json).</item>
    /// <item>Os uivos dos cachorros e as placas de metal estão nas notas do ré menor da trilha.</item>
    /// <item>Cada som vem de um lugar em volta de quem escuta (a janela, numa fachada de verdade, achada por um raio; os
    /// cachorros, longe; os corvos, no alto), abafado pela distância e pelo que estiver no meio (Combat/Abafamento).</item>
    /// <item>À noite, os grilos (em laço). De dentro de um prédio ou de uma casa (ou debaixo de um teto), o vento e os
    /// grilos chegam abafados, e quem range é a madeira.</item>
    /// </list>
    /// Os sons saem do Ferramentas/ambiente.py (Audio/Ambiente) e entram no TrilhaDoJogo pelo menu Montar a trilha.
    /// </summary>
    public partial class MaestroDaTrilha
    {
        [Header("O ambiente")]
        [Tooltip("O volume dos grilos (à noite, fora).")]
        public float volumeDosGrilos = 0.35f;
        [Tooltip("No silêncio, um som de ambiente a cada tantos segundos (mínimo, máximo).")]
        public Vector2 ambienteNoSilencio = new Vector2(9f, 20f);
        [Tooltip("Com a música da exploração, um a cada tantos segundos.")]
        public Vector2 ambienteNaMusica = new Vector2(25f, 50f);
        [Tooltip("A chance de uma rajada do vento bater uma janela, uma porta, a placa ou a lata.")]
        [Range(0f, 1f)] public float chanceNaRajada = 0.6f;
        [Tooltip("Com a música da exploração tocando, o ambiente fica a este tanto do volume.")]
        [Range(0f, 1f)] public float ambienteComMusica = 0.65f;

        static float _volumeDoAmbiente = -1f;

        /// <summary>
        /// O volume dos sons de ambiente (Configurações > Sons de ambiente, a pedido em 29/10/2026; de 0 a 1, gravado no
        /// computador), separado da música: os grilos mudam na hora; os eventos, a partir do próximo. Em 0, o ambiente para
        /// (o vento da trilha continua: ele é da música).
        /// </summary>
        public static float VolumeDoAmbiente
        {
            get
            {
                // antes era só ligado ou desligado ("ambiente.ligado"): quem desligou começa em 0
                if (_volumeDoAmbiente < 0f)
                    _volumeDoAmbiente = Mathf.Clamp01(PlayerPrefs.GetFloat("ambiente.volume", PlayerPrefs.GetInt("ambiente.ligado", 1) == 1 ? 1f : 0f));
                return _volumeDoAmbiente;
            }
            set
            {
                _volumeDoAmbiente = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat("ambiente.volume", _volumeDoAmbiente);   // grava junto com as opções
            }
        }

        /// <summary>O ambiente ligado (o volume acima de 0; o botão do F1 liga em 100% ou desliga).</summary>
        public static bool AmbienteLigado
        {
            get => VolumeDoAmbiente > 0.001f;
            set { VolumeDoAmbiente = value ? 1f : 0f; PlayerPrefs.Save(); }
        }

        /// <summary>O último som de ambiente e por quê (o F1 mostra).</summary>
        public string UltimoAmbiente { get; private set; } = "";
        /// <summary>De 0 (fora) a 1 (dentro de um prédio, de uma casa ou debaixo de um teto).</summary>
        public float Dentro => _dentro;

        class Evento
        {
            public AudioSource som;
            public double fim;
        }

        float _dentro, _alvoDentro, _proximaOlhadaDentro, _volumeDosGrilosAgora;
        double _proximoEvento;
        string _ultimoTipo;
        AudioSource _grilos;
        AudioLowPassFilter _filtroDosGrilos;
        readonly List<double> _rajadas = new List<double>();
        readonly List<Evento> _eventos = new List<Evento>();
        readonly List<AudioSource> _livresDoAmbiente = new List<AudioSource>();

        // dentro, o vento chega abafado
        float CorteDoVento => Abafamento.Corte(_dentro * 0.85f);

        void ComecarOAmbiente(double agora)
        {
            _rajadas.Clear();
            _proximoEvento = agora + Random.Range(8f, 15f);
            _ultimoTipo = null;
            UltimoAmbiente = "";
        }

        void DesligarOAmbiente()
        {
            _rajadas.Clear();
            if (_grilos != null) _grilos.Stop();
            _volumeDosGrilosAgora = 0f;
            foreach (var e in _eventos)
            {
                e.som.Stop();
                _livresDoAmbiente.Add(e.som);
            }
            _eventos.Clear();
        }

        // o vento da trilha marcou um bloco: as rajadas dele, no relógio do áudio
        void MarcarRajadas(TrilhaDoJogo.Rajadas[] rajadas, int i, double inicio)
        {
            if (rajadas == null || i < 0 || i >= rajadas.Length || rajadas[i] == null || rajadas[i].tempos == null) return;
            foreach (float t in rajadas[i].tempos) _rajadas.Add(inicio + t);
        }

        // a cada quadro, na partida
        void Ambiente(double agora, bool dia, float dt)
        {
            if (Time.unscaledTime >= _proximaOlhadaDentro)
            {
                _proximaOlhadaDentro = Time.unscaledTime + 0.5f;
                _alvoDentro = EstaDentro() ? 1f : 0f;
            }
            _dentro = Mathf.MoveTowards(_dentro, _alvoDentro, dt / 1.2f);
            bool ligado = AmbienteLigado;
            Grilos(dt, ligado);
            for (int i = _eventos.Count - 1; i >= 0; i--)
            {
                if (agora < _eventos[i].fim + 0.1) continue;
                _eventos[i].som.Stop();
                _livresDoAmbiente.Add(_eventos[i].som);
                _eventos.RemoveAt(i);
            }
            if (!ligado)
            {
                _rajadas.Clear();
                return;
            }
            bool quieto = _estado == Estado.Perigo || MomentoTocando(agora);
            // as rajadas: o que o vento mexe bate logo depois de ela passar
            for (int i = _rajadas.Count - 1; i >= 0; i--)
            {
                if (agora < _rajadas[i]) continue;
                _rajadas.RemoveAt(i);
                if (!quieto && Random.value < chanceNaRajada) TocarEvento(DoVento(), agora + Random.Range(0.2f, 1.0f), "rajada");
            }
            if (quieto)
            {
                if (_proximoEvento < agora + 6) _proximoEvento = agora + 6;   // depois do perigo, um respiro antes
                return;
            }
            if (agora >= _proximoEvento)
            {
                var faixa = _estado == Estado.Exploracao ? ambienteNaMusica : ambienteNoSilencio;
                _proximoEvento = agora + Random.Range(faixa.x, faixa.y);
                TocarEvento(Sortear(dia), agora + 0.05, "solto");
            }
        }

        bool MomentoTocando(double agora)
        {
            foreach (var f in _fontes)
                if (f.grupo == "momento" && !f.cortar && agora < f.fim - 2.0) return true;
            return false;
        }

        // dentro de um prédio ou de uma casa (a caixa de dentro), ou com um teto em cima (até 25 m)
        static bool EstaDentro()
        {
            if (!Abafamento.Ouvido(out var ouvido)) return false;
            return AreaInterna.Contem(ouvido) || Physics.Raycast(ouvido, Vector3.up, 25f, Abafamento.Mascara, QueryTriggerInteraction.Ignore);
        }

        // ------------------------------------------------------------------ os grilos (à noite, em laço)
        void Grilos(float dt, bool ligado)
        {
            float noite = Mathf.InverseLerp(0.3f, 0.8f, CicloDoDia.Noite);
            float musica = _estado == Estado.Perigo ? 0.4f : _estado == Estado.Exploracao ? 0.6f : 1f;
            float alvo = ligado && _trilha.grilos != null && _trilha.grilos.Length > 0 ? volumeDosGrilos * noite * musica * Mathf.Lerp(1f, 0.35f, _dentro) : 0f;
            _volumeDosGrilosAgora = Mathf.MoveTowards(_volumeDosGrilosAgora, alvo, dt * 0.12f);
            if (_volumeDosGrilosAgora <= 0.001f)
            {
                if (_grilos != null && _grilos.isPlaying) _grilos.Stop();
                return;
            }
            if (_grilos == null)
            {
                var go = new GameObject("Grilos");
                go.transform.SetParent(transform, false);
                _grilos = go.AddComponent<AudioSource>();
                _grilos.loop = true;
                _grilos.playOnAwake = false;
                _grilos.spatialBlend = 0f;
                _grilos.priority = 10;
                _grilos.bypassReverbZones = true;
                _filtroDosGrilos = go.AddComponent<AudioLowPassFilter>();
            }
            if (!_grilos.isPlaying)
            {
                _grilos.clip = _trilha.grilos[Random.Range(0, _trilha.grilos.Length)];
                _grilos.time = Random.Range(0f, _grilos.clip.length * 0.9f);
                _grilos.Play();
            }
            _grilos.volume = _volumeDosGrilosAgora * VolumeDoAmbiente;
            _filtroDosGrilos.cutoffFrequency = Abafamento.Corte(_dentro * 0.9f);
        }

        // ------------------------------------------------------------------ os eventos
        AudioClip[] Clipes(string tipo)
        {
            switch (tipo)
            {
                case "latido": return _trilha.latidos;
                case "uivo": return _trilha.uivos;
                case "janela": return _trilha.janelas;
                case "porta": return _trilha.portas;
                case "placa": return _trilha.placas;
                case "lata": return _trilha.latas;
                case "madeira": return _trilha.madeira;
                case "corvos": return _trilha.corvos;
            }
            return null;
        }

        static string Pesar(params (string tipo, float peso)[] opcoes)
        {
            float total = 0f;
            foreach (var o in opcoes) total += o.peso;
            float r = Random.value * total;
            foreach (var o in opcoes)
            {
                r -= o.peso;
                if (r <= 0f) return o.tipo;
            }
            return opcoes[opcoes.Length - 1].tipo;
        }

        // o que a rajada mexe
        string DoVento() => _dentro > 0.5f
            ? Pesar(("madeira", 0.5f), ("janela", 0.3f), ("porta", 0.2f))
            : Pesar(("janela", 0.4f), ("placa", 0.3f), ("porta", 0.15f), ("lata", 0.15f));

        // um som solto, pelo lugar e pela hora (sem repetir o tipo do último)
        string Sortear(bool dia)
        {
            for (int tentativa = 0; tentativa < 3; tentativa++)
            {
                string t = _dentro > 0.5f
                    ? Pesar(("madeira", 0.5f), ("porta", 0.2f), (dia ? "latido" : "uivo", 0.15f), ("latido", 0.15f))
                    : dia ? Pesar(("corvos", 0.35f), ("latido", 0.25f), ("janela", 0.15f), ("placa", 0.15f), ("porta", 0.1f))
                          : Pesar(("latido", 0.35f), ("uivo", 0.25f), ("janela", 0.15f), ("placa", 0.15f), ("porta", 0.1f));
                if (t != _ultimoTipo) return t;
            }
            return _ultimoTipo ?? "latido";
        }

        void TocarEvento(string tipo, double quando, string porque)
        {
            var lista = Clipes(tipo);
            if (lista == null || lista.Length == 0 || !Abafamento.Ouvido(out var ouvido)) return;
            var clipe = Escolher("ambiente_" + tipo, new List<AudioClip>(lista));
            if (clipe == null) return;
            Vector2 giro = Random.insideUnitCircle.normalized;
            var direcao = new Vector3(giro.x, 0f, giro.y);
            Vector3 onde;
            float alcance, volume;
            switch (tipo)
            {
                case "latido":
                case "uivo":
                    onde = ouvido + direcao * Random.Range(45f, 75f) + Vector3.down * 1.5f;
                    alcance = 140f;
                    volume = tipo == "latido" ? 0.85f : 0.7f;
                    break;
                case "corvos":
                    onde = ouvido + direcao * Random.Range(20f, 45f) + Vector3.up * Random.Range(10f, 18f);
                    alcance = 90f;
                    volume = 0.6f;
                    break;
                case "janela":
                case "porta":
                case "placa":
                    // numa fachada de verdade, se houver uma em volta (até 30 m); senão, no ar, a uma distância de rua
                    if (Physics.Raycast(ouvido, direcao, out var fachada, 30f, Abafamento.Mascara, QueryTriggerInteraction.Ignore) && fachada.distance > 2f)
                        onde = fachada.point + fachada.normal * 0.3f + Vector3.up * Random.Range(0f, 4f);
                    else
                        onde = ouvido + direcao * Random.Range(12f, 25f) + Vector3.up * Random.Range(1f, 4f);
                    alcance = tipo == "placa" ? 60f : 70f;
                    volume = tipo == "janela" ? 0.8f : tipo == "porta" ? 0.9f : 0.45f;
                    break;
                case "lata":
                    onde = ouvido + direcao * Random.Range(6f, 18f) + Vector3.down * 1.5f;
                    alcance = 40f;
                    volume = 0.5f;
                    break;
                default:   // a madeira: perto, dentro
                    onde = ouvido + Random.insideUnitSphere * 3f + direcao * Random.Range(3f, 6f);
                    alcance = 25f;
                    volume = 0.6f;
                    break;
            }
            var som = PegarSomDoAmbiente();
            som.transform.position = onde;
            som.clip = clipe;
            som.maxDistance = alcance;
            som.bypassReverbZones = tipo != "madeira";   // os de fora já trazem o eco da rua
            float distancia = Vector3.Distance(ouvido, onde);
            float tapado = Abafamento.Quanto(ouvido, onde);
            if (distancia > 30f) tapado *= 0.6f;   // de longe, o som passa por cima dos telhados: nunca chega todo tapado
            som.GetComponent<AudioLowPassFilter>().cutoffFrequency =
                Mathf.Min(22000f * Mathf.Pow(0.15f, Mathf.Clamp01(distancia / alcance)), Abafamento.Corte(tapado));
            float comMusica = _estado == Estado.Exploracao ? ambienteComMusica : 1f;
            som.volume = volume * comMusica * Mathf.Lerp(1f, Abafamento.VolumeTapado, tapado) * VolumeDoAmbiente;
            som.PlayScheduled(System.Math.Max(quando, AudioSettings.dspTime + 0.02));
            _eventos.Add(new Evento { som = som, fim = quando + clipe.length });
            _ultimoTipo = tipo;
            UltimoAmbiente = $"{tipo} ({porque}, {distancia:0} m{(tapado > 0.5f ? ", abafado" : "")})";
        }

        AudioSource PegarSomDoAmbiente()
        {
            if (_livresDoAmbiente.Count > 0)
            {
                var s = _livresDoAmbiente[_livresDoAmbiente.Count - 1];
                _livresDoAmbiente.RemoveAt(_livresDoAmbiente.Count - 1);
                return s;
            }
            var go = new GameObject("Ambiente");
            go.transform.SetParent(transform, false);
            var som = go.AddComponent<AudioSource>();
            som.playOnAwake = false;
            som.loop = false;
            som.spatialBlend = 1f;
            som.rolloffMode = AudioRolloffMode.Linear;
            som.minDistance = 3f;
            som.dopplerLevel = 0f;
            som.priority = 128;
            go.AddComponent<AudioLowPassFilter>();
            return som;
        }

        /// <summary>Pelo F1: um som de ambiente agora (o que caberia aqui, de dia ou de noite, dentro ou fora).</summary>
        public void AmbienteAgora()
        {
            if (_trilha == null || _estado == Estado.Fora) return;
            TocarEvento(Sortear(CicloDoDia.Noite < 0.5f), AudioSettings.dspTime + 0.05, "F1");
        }
    }
}
