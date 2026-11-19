using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Loot;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Os instrumentos da fogueira (18/11/2026, a etapa 4, a pedido: "instrumentos musicais (violão, pandeiro, gaita, flauta)
    /// para tocar em volta da fogueira ... seria interessante existir algumas músicas, que quando cada um toca o instrumento,
    /// a música vai se compondo e se encaixando"). Três músicas originais (Trabalho-Claude/musicas/compor.py, das amostras
    /// CC0 da FreePats e da VCSL), cada uma em quatro faixas do mesmo tamanho (Resources/Musicas/&lt;música&gt;_&lt;instrumento&gt;):
    /// - o dono, com o instrumento na mão: o clique começa e para; quem já toca a até 15 m puxa a roda, e quem chega entra
    ///   na mesma música, no mesmo compasso (o começo dela no relógio do servidor). O botão direito troca a música (só quem
    ///   puxa a roda: o de menor número entre os que tocam ali; os outros seguem). Trocar de item, cair, entrar num carro ou
    ///   nadar para de tocar;
    /// - todas as cópias: a faixa do instrumento dele toca no corpo (3D), em laço, acertada pelo relógio (a cada meio segundo,
    ///   se escapou mais de 60 ms);
    /// - o anfitrião: a música faz barulho (a cada 3 s, uns 30 m: os zumbis vêm ver; perto, vêm atrás de quem toca), e quem
    ///   ouve a até 12 m acalma a cabeça (VidaJogador.Mente.cs, o motivo Musica: mais com mais gente tocando).
    /// </summary>
    [DefaultExecutionOrder(104)]
    public class MusicaDoJogador : MonoBehaviour
    {
        public static readonly string[] Musicas = { "estrada", "acampamento", "rio" };
        public static readonly string[] Titulos = { "Estrada de Terra", "Noite no Acampamento", "Rio Frio" };
        static readonly string[] Faixas = { "", "violao", "pandeiro", "gaita", "flauta" };
        /// <summary>O andamento de cada música (as batidas por minuto do compor.py): a pose de quem toca vai no tempo dela.</summary>
        public static readonly float[] Bpm = { 104f, 96f, 76f };

        /// <summary>Em que tempo da música ele está (1 a cada batida, desde o começo; a pose: PassageiroDoCarro.Instrumento.cs).</summary>
        public static float Batida(MusicaTocando t) => Mathf.Max(0f, Agora - t.inicio) * Bpm[t.musica % Bpm.Length] / 60f;

        /// <summary>Até onde quem toca entra na mesma música (m).</summary>
        public const float Roda = 15f;
        /// <summary>Até onde a música acalma quem ouve (m).</summary>
        public const float AlcanceDaMente = 12f;
        /// <summary>Até onde o barulho da música chega aos zumbis (m), a cada <see cref="CadaBarulho"/> s.</summary>
        public const float Barulho = 30f, CadaBarulho = 3f;

        /// <summary>O número do instrumento (1 a 4), ou 0.</summary>
        public static int InstrumentoDe(Item i) => i switch
        {
            Item.Violao => 1,
            Item.Pandeiro => 2,
            Item.Gaita => 3,
            Item.Flauta => 4,
            _ => 0,
        };

        public static bool EhInstrumento(Item i) => InstrumentoDe(i) > 0;

        /// <summary>Quantos tocam a até 'alcance' m do ponto (a mente, no anfitrião).</summary>
        public static int TocandoPerto(Vector3 p, float alcance)
        {
            int n = 0;
            foreach (var j in JogadorRede.Todos)
                if (j != null && j.IsSpawned && j.Tocando.Value.instrumento != 0 && (j.transform.position - p).sqrMagnitude <= alcance * alcance) n++;
            return n;
        }

        JogadorRede _rede;
        AudioSource _fonte;
        AudioClip _clipe;
        string _nomeDoClipe;
        float _proximoAcerto, _proximoSeguir, _proximoBarulho;
        static int _proximaMusica = -1;

        static float Agora => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening ? NetworkManager.Singleton.ServerTime.TimeAsFloat : Time.time;

        void Awake() => _rede = GetComponent<JogadorRede>();

        void OnDisable()
        {
            if (_fonte != null) _fonte.Stop();
        }

        // ------------------------------------------------------------------ o dono

        /// <summary>O clique com o instrumento na mão: começa (entrando na roda de perto) ou para.</summary>
        public void Alternar()
        {
            if (_rede == null || !_rede.IsOwner || _rede.Inventario == null) return;
            if (_rede.Tocando.Value.instrumento != 0)
            {
                Parar();
                InventarioJogador.AvisoLocal("Parou de tocar");
                return;
            }
            int inst = InstrumentoDe(_rede.Inventario.NaMao);
            if (inst == 0 || !PodeTocar()) return;
            var roda = Lider(false);
            MusicaTocando novo;
            if (roda != null)
            {
                var r = roda.Tocando.Value;
                novo = new MusicaTocando { instrumento = (byte)inst, musica = r.musica, inicio = r.inicio };
            }
            else
            {
                if (_proximaMusica < 0) _proximaMusica = Random.Range(0, Musicas.Length);
                novo = new MusicaTocando { instrumento = (byte)inst, musica = (byte)_proximaMusica, inicio = Agora };
                _proximaMusica = (_proximaMusica + 1) % Musicas.Length;
            }
            _rede.Tocando.Value = novo;
            InventarioJogador.AvisoLocal(roda != null ? $"Entrou na roda: {Titulos[novo.musica % Musicas.Length]}   ·   [clique] Parar"
                : $"Tocando {Titulos[novo.musica % Musicas.Length]}   ·   [clique] Parar   ·   [botão direito] Outra música");
        }

        /// <summary>O botão direito tocando: a próxima música (só quem puxa a roda; os outros seguem).</summary>
        public void OutraMusica()
        {
            if (_rede == null || !_rede.IsOwner) return;
            var t = _rede.Tocando.Value;
            if (t.instrumento == 0) return;
            var lider = Lider(true);
            if (lider != null && lider != _rede)
            {
                InventarioJogador.AvisoLocal("Quem começou a roda escolhe a música");
                return;
            }
            t.musica = (byte)((t.musica + 1) % Musicas.Length);
            t.inicio = Agora;
            _rede.Tocando.Value = t;
            InventarioJogador.AvisoLocal($"Tocando {Titulos[t.musica]}");
        }

        void Parar()
        {
            if (_rede != null && _rede.IsOwner && _rede.Tocando.Value.instrumento != 0) _rede.Tocando.Value = default;
        }

        bool PodeTocar() =>
            (_rede.Vida == null || !_rede.Vida.Caido.Value) && (_rede.Passageiro == null || !_rede.Passageiro.NoCarro)
            && !(_rede.TryGetComponent<MovimentoPrimeiraPessoa>(out var mov) && mov.Nadando);

        /// <summary>
        /// Quem puxa a roda: o de menor número entre os que tocam a até 15 m (com 'euConto', ele também). Null: ninguém
        /// tocando perto.
        /// </summary>
        JogadorRede Lider(bool euConto)
        {
            JogadorRede melhor = null;
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || !j.IsSpawned || j.Tocando.Value.instrumento == 0) continue;
                if (j == _rede && !euConto) continue;
                if ((j.transform.position - transform.position).sqrMagnitude > Roda * Roda) continue;
                if (melhor == null || j.OwnerClientId < melhor.OwnerClientId) melhor = j;
            }
            return melhor;
        }

        void Update()
        {
            if (_rede == null || !_rede.IsSpawned) return;
            var t = _rede.Tocando.Value;
            if (_rede.IsOwner && t.instrumento != 0)
            {
                // trocou de item (ou não pode mais): para
                if (_rede.Inventario == null || InstrumentoDe(_rede.Inventario.NaMao) != t.instrumento || !PodeTocar()) Parar();
                // segue quem puxa a roda (a música e o compasso dele)
                else if (Time.time >= _proximoSeguir)
                {
                    _proximoSeguir = Time.time + 0.5f;
                    var lider = Lider(true);
                    if (lider != null && lider != _rede)
                    {
                        var l = lider.Tocando.Value;
                        if (l.musica != t.musica || !Mathf.Approximately(l.inicio, t.inicio))
                        {
                            t.musica = l.musica;
                            t.inicio = l.inicio;
                            _rede.Tocando.Value = t;
                        }
                    }
                }
            }
            // o anfitrião: o barulho chama os zumbis
            if (_rede.IsServer && t.instrumento != 0 && Time.time >= _proximoBarulho)
            {
                _proximoBarulho = Time.time + CadaBarulho;
                ZombieManager.Barulho(transform.position + Vector3.up, _rede, Barulho);
            }
            Tocar(t);
        }

        // ------------------------------------------------------------------ o som (cada cópia)

        void Tocar(MusicaTocando t)
        {
            if (t.instrumento == 0 || t.instrumento >= Faixas.Length)
            {
                if (_fonte != null && _fonte.isPlaying) _fonte.Stop();
                return;
            }
            string nome = $"Musicas/{Musicas[t.musica % Musicas.Length]}_{Faixas[t.instrumento]}";
            if (nome != _nomeDoClipe)
            {
                _nomeDoClipe = nome;
                _clipe = Resources.Load<AudioClip>(nome);
                if (_clipe == null) Debug.LogWarning($"[Música] Sem a faixa {nome} (Resources/Musicas)");
                if (_fonte == null) _fonte = CriarAFonte();
                _fonte.Stop();
                _fonte.clip = _clipe;
                _proximoAcerto = 0f;
            }
            if (_clipe == null) return;
            // a faixa ainda carregando: espera (tocar antes deixava a primeira atrasada; 18/11/2026)
            if (_clipe.loadState != AudioDataLoadState.Loaded)
            {
                if (_clipe.loadState == AudioDataLoadState.Unloaded) _clipe.LoadAudioData();
                return;
            }
            // nas amostras (o 'time' em segundos é impreciso no áudio comprimido)
            int total = _clipe.samples, freq = _clipe.frequency;
            int certo = Mathf.Clamp((int)(Mathf.Repeat(Agora - t.inicio, _clipe.length) * freq), 0, total - 1);
            if (!_fonte.isPlaying)
            {
                _fonte.Play();
                _fonte.timeSamples = certo;
                _proximoAcerto = Time.time + 0.5f;
                return;
            }
            if (Time.time < _proximoAcerto) return;
            _proximoAcerto = Time.time + 0.5f;
            int diferenca = _fonte.timeSamples - certo;
            if (diferenca > total / 2) diferenca -= total;
            else if (diferenca < -total / 2) diferenca += total;
            if (Mathf.Abs(diferenca) > (int)(0.06f * freq)) _fonte.timeSamples = certo;
        }

        AudioSource CriarAFonte()
        {
            var go = new GameObject("Música");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1.1f, 0.25f);
            var f = go.AddComponent<AudioSource>();
            f.playOnAwake = false;
            f.loop = true;
            f.spatialBlend = 1f;
            f.rolloffMode = AudioRolloffMode.Logarithmic;
            f.minDistance = 3f;
            f.maxDistance = 45f;
            f.dopplerLevel = 0f;
            f.volume = 0.85f;
            return f;
        }
    }
}
