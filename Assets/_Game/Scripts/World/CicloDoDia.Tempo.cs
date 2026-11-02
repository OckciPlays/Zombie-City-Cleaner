using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Player;
using ZombieCityCleaner.Video;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.World
{
    /// <summary>Os níveis do tempo (a pedido em 02/11/2026). O sol é o padrão; o resto é um evento que passa.</summary>
    public enum NivelDoTempo : byte { Sol, Chuvisco, Chuva, Tempestade, Raios }

    /// <summary>Um raio, para quem desenha e toca (o <see cref="ClimaNaTela"/>, em cada máquina).</summary>
    public struct Raio
    {
        /// <summary>Onde caiu (no chão, no telhado, no topo do poste...).</summary>
        public Vector3 ponto;
        /// <summary>0: longe (só o clarão no céu e o trovão); 1: perto (o raio aparece); 2: direto em alguém.</summary>
        public byte tipo;
    }

    /// <summary>
    /// O tempo agora, nesta máquina (suavizado), para qualquer script ler: as nuvens, a chuva, o vento e as poças (de 0 a 1),
    /// o nível, o tornado e o clarão dos raios. Quem decide é o anfitrião (<see cref="CicloDoDia"/>, CicloDoDia.Tempo.cs).
    /// </summary>
    public static class Tempo
    {
        public static NivelDoTempo Nivel { get; internal set; }
        public static float Nuvens { get; internal set; }
        public static float Chuva { get; internal set; }
        public static float Vento { get; internal set; }
        /// <summary>O quanto as poças estão cheias (sobem na chuva forte, secam devagar depois).</summary>
        public static float Pocas { get; internal set; }
        public static bool Tornado { get; internal set; }
        public static Vector3 PosicaoDoTornado { get; internal set; }
        /// <summary>De 0 a 1: o tornado descendo, inteiro e se desfazendo.</summary>
        public static float ForcaDoTornado { get; internal set; }

        /// <summary>
        /// O sol que chega aos zumbis (a pedido: "com nuvens o sol não petrifica"): 1 no céu aberto, pela metade no chuvisco,
        /// 0 na chuva e na tempestade.
        /// </summary>
        public static float Sol => 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.8f, Nuvens));

        /// <summary>O quanto a chuva abafa o barulho para os zumbis (1 nada; 0,55 na tempestade): o tiro e os passos.</summary>
        public static float Ouvido => 1f - 0.45f * Mathf.Clamp01(Chuva);

        /// <summary>O clarão dos raios agora (o ClimaNaTela põe; o AmbienteDoDia acende o céu e a luz).</summary>
        public static float Clarao { get; set; }

        /// <summary>Um raio caiu (em cada máquina, pela rede).</summary>
        public static event System.Action<Raio> AoCairUmRaio;

        internal static void Avisar(Raio r) => AoCairUmRaio?.Invoke(r);

        internal static void Zerar()
        {
            Nivel = NivelDoTempo.Sol;
            Nuvens = Chuva = Vento = Pocas = 0f;
            Tornado = false;
            ForcaDoTornado = 0f;
            Clarao = 0f;
        }

        public static string NomeDo(NivelDoTempo n) => n switch
        {
            NivelDoTempo.Chuvisco => "chuvisco",
            NivelDoTempo.Chuva => "chuva",
            NivelDoTempo.Tempestade => "tempestade",
            NivelDoTempo.Raios => "tempestade com raios",
            _ => "sol",
        };
    }

    /// <summary>
    /// O tempo (o clima; a pedido em 02/11/2026). O anfitrião manda: segue um calendário de frentes, como uma "mini-estação",
    /// "uma nuvem que demora pra passar": a calmaria (dias de sol), a frente fraca (um dia de chuva e um de chuvisco), a
    /// calmaria, a frente forte (um dia de tempestade e outro de tempestade com raios, com o tornado no fim em
    /// <see cref="chanceDoTornado"/> das vezes) e a calmaria de novo; a ordem e as durações variam a cada ciclo, e os primeiros
    /// dias são de sol. Cada evento ocupa só umas horas do dia, com os sinais antes (as nuvens e o vento sobem) e o fim aos
    /// poucos. O anfitrião também sorteia os raios (e o que eles fazem: os zumbis viram cinza, o alarme do carro dispara, o
    /// raio direto mata) e leva o tornado pelo caminho dele (o dano, os zumbis arrastados). Os outros recebem o estado a cada
    /// segundo e os raios na hora. O que se vê e se ouve é do <see cref="ClimaNaTela"/>.
    /// </summary>
    public partial class CicloDoDia
    {
        [Header("O tempo (o clima)")]
        [Tooltip("Sem o clima, é sempre sol.")]
        public bool climaLigado = true;
        [Tooltip("O ritmo de teste (02/11/2026, o teste com os amigos): as calmarias e os dias da frente bem mais curtos, para " +
                 "ver o ciclo inteiro, com o tornado, em uma ou duas horas. Desligado, o ritmo de verdade: dias de sol, a chuva rara.")]
        public bool ritmoDeTeste = true;
        [Tooltip("A chance do tornado no fim de uma frente forte. O tornado é um evento bem raro no jogo; 0,6 é para o teste.")]
        [Range(0f, 1f)] public float chanceDoTornado = 0.6f;

        /// <summary>As nuvens, a chuva, o vento e as poças (de 0 a 1), do anfitrião, a cada segundo.</summary>
        readonly NetworkVariable<Vector4> _tempoRede = new NetworkVariable<Vector4>();
        readonly NetworkVariable<byte> _nivelRede = new NetworkVariable<byte>();
        /// <summary>O tornado: onde está (x, y, z) e a força (w; 0 sem tornado).</summary>
        readonly NetworkVariable<Vector4> _tornadoRede = new NetworkVariable<Vector4>();

        struct Evento
        {
            public double inicio, fim;   // em horas do jogo, desde o começo da partida
            public NivelDoTempo nivel;
            public bool tornado;
        }

        readonly List<Evento> _calendario = new List<Evento>();
        System.Random _sorteio;
        double _horas;               // as horas do jogo desde o começo do clima nesta partida
        float _nuvens, _chuva, _vento, _pocas;
        int _forcado = -1;            // o nível forçado pelo F1 (-1: o do calendário)
        float _proximoEnvioDoTempo, _proximoRaioLonge, _proximoRaioPerto, _proximoGolpeDoTornado, _proximoEnvioDoTornado;
        readonly Dictionary<JogadorRede, float> _danoDoTornado = new Dictionary<JogadorRede, float>();

        // o tornado (no anfitrião)
        bool _tornado;
        Vector3 _tornadoPos, _tornadoDe, _tornadoMeio, _tornadoAte;
        float _tornadoT, _tornadoDuracao, _tornadoForca, _tornadoAntes, _tornadoVel, _tornadoAcabaEm = -1f;
        bool _tornadoDoEventoFeito;

        /// <summary>O que o anfitrião planejou (para o F1): o evento de agora ou o próximo, e em quanto tempo.</summary>
        public string ResumoDoTempo()
        {
            string agora = $"Agora: {Tempo.NomeDo(Tempo.Nivel)}{(_forcado >= 0 ? " (forçado)" : "")}\n" +
                           $"nuvens {Tempo.Nuvens:0.00} · chuva {Tempo.Chuva:0.00}\nvento {Tempo.Vento:0.00} · poças {Tempo.Pocas:0.00}";
            if (Tempo.Tornado) agora += $"\ntornado a {DistanciaAoTornado():0} m (força {Tempo.ForcaDoTornado:0.00})";
            if (!IsSpawned || !IsServer || _calendario.Count == 0) return agora;
            foreach (var e in _calendario)
            {
                if (e.fim < _horas) continue;
                double falta = (e.inicio - _horas) * minutosPorDia / 24.0;   // minutos reais
                string quando = falta > 0 ? $"em {falta:0} min" : "agora";
                return agora + $"\nPróximo: {Tempo.NomeDo(e.nivel)}{(e.tornado ? " + tornado" : "")} {quando} ({(e.fim - e.inicio):0.0} h)";
            }
            return agora;
        }

        float DistanciaAoTornado()
        {
            var cam = Camera.main;
            return cam != null ? Vector3.Distance(cam.transform.position, Tempo.PosicaoDoTornado) : -1f;
        }

        void TempoAoComecar()
        {
            Tempo.Zerar();
            _calendario.Clear();
            _horas = 0;
            _nuvens = _chuva = _vento = _pocas = 0f;
            _forcado = -1;
            _tornado = false;
            _tornadoDoEventoFeito = false;
            _sorteio = null;   // sorteado no primeiro ciclo, pela semente do mundo (aqui ela ainda pode não ter chegado)
            if (!IsServer) return;
            _tempoRede.Value = Vector4.zero;
            _nivelRede.Value = 0;
            _tornadoRede.Value = Vector4.zero;
        }

        void TempoAoAcabar() => Tempo.Zerar();

        // ------------------------------------------------------------------------------------------ o calendário

        double Sortear(double de, double ate) => de + (ate - de) * _sorteio.NextDouble();

        /// <summary>Mais um ciclo no fim do calendário: a calmaria, as frentes (em ordem que varia) com calmarias no meio.</summary>
        void PlanejarCiclo()
        {
            if (_sorteio == null)
            {
                var mundo = MundoDaPartida.Instancia;
                int semente = mundo != null && mundo.IsSpawned && mundo.Semente != 0 ? mundo.Semente : Random.Range(1, 99999);
                _sorteio = new System.Random(semente * 7919 + 17);
            }
            bool teste = ritmoDeTeste;
            double t = _calendario.Count > 0 ? _calendario[_calendario.Count - 1].fim : _horas;
            bool primeiro = _calendario.Count == 0;
            // a calmaria antes: os primeiros dias são de sol (para pegar o jogo); depois, de 4 a 7 dias
            t += primeiro ? (teste ? Sortear(7, 11) : Sortear(72, 96)) : (teste ? Sortear(10, 18) : Sortear(96, 168));
            // as frentes do ciclo: uma fraca e uma forte, às vezes uma fraca a mais, em ordem sorteada
            var frentes = new List<bool> { false, true };
            if (_sorteio.NextDouble() < 0.4) frentes.Add(false);
            for (int i = frentes.Count - 1; i > 0; i--)
            {
                int j = _sorteio.Next(i + 1);
                (frentes[i], frentes[j]) = (frentes[j], frentes[i]);
            }
            for (int f = 0; f < frentes.Count; f++)
            {
                if (f > 0) t += teste ? Sortear(6, 12) : Sortear(48, 96);   // a calmaria curta entre as frentes
                NivelDoTempo a, b;
                if (frentes[f])
                {
                    a = NivelDoTempo.Tempestade;
                    b = NivelDoTempo.Raios;
                }
                else if (_sorteio.NextDouble() < 0.5)
                {
                    a = NivelDoTempo.Chuva;
                    b = NivelDoTempo.Chuvisco;
                }
                else
                {
                    a = NivelDoTempo.Chuvisco;
                    b = NivelDoTempo.Chuva;
                }
                // o primeiro dia da frente e o seguinte (cada um só umas horas, em qualquer hora do dia)
                double inicioA = t + (teste ? Sortear(0, 2) : Sortear(0, 18));
                double fimA = inicioA + Duracao(a, teste);
                double inicioB = fimA + (teste ? Sortear(3, 6) : Sortear(12, 26));
                double fimB = inicioB + Duracao(b, teste);
                _calendario.Add(new Evento { inicio = inicioA, fim = fimA, nivel = a });
                _calendario.Add(new Evento { inicio = inicioB, fim = fimB, nivel = b, tornado = frentes[f] && _sorteio.NextDouble() < chanceDoTornado });
                t = fimB;
            }
        }

        double Duracao(NivelDoTempo n, bool teste)
        {
            double h = n switch
            {
                NivelDoTempo.Chuvisco => Sortear(3, 5),
                NivelDoTempo.Chuva => Sortear(4, 6),
                _ => Sortear(4, 7),
            };
            return teste ? h * 0.7 : h;
        }

        // o evento de agora (com os sinais antes e o fim aos poucos) e o quanto ele já pegou (0 a 1)
        bool EventoAgora(out Evento e, out float envoltorio, out float dentro)
        {
            const double Antes = 1.5, Depois = 1.0;
            foreach (var ev in _calendario)
            {
                if (_horas < ev.inicio - Antes || _horas > ev.fim + Depois) continue;
                e = ev;
                dentro = (float)((_horas - ev.inicio) / System.Math.Max(0.01, ev.fim - ev.inicio));
                envoltorio = _horas < ev.inicio ? (float)(1.0 - (ev.inicio - _horas) / Antes)
                    : _horas > ev.fim ? (float)(1.0 - (_horas - ev.fim) / Depois) : 1f;
                envoltorio = Mathf.Clamp01(envoltorio);
                return true;
            }
            e = default;
            envoltorio = 0f;
            dentro = 0f;
            return false;
        }

        // o que cada nível traz: as nuvens, a chuva e o vento
        static Vector3 Alvo(NivelDoTempo n) => n switch
        {
            NivelDoTempo.Chuvisco => new Vector3(0.62f, 0.28f, 0.2f),
            NivelDoTempo.Chuva => new Vector3(0.85f, 0.62f, 0.35f),
            NivelDoTempo.Tempestade => new Vector3(1f, 0.9f, 0.7f),
            NivelDoTempo.Raios => new Vector3(1f, 1f, 0.85f),
            _ => Vector3.zero,
        };

        // ------------------------------------------------------------------------------------------ a cada quadro

        void AtualizarTempo()
        {
            if (!IsSpawned)
            {
                Tempo.Zerar();
                return;
            }
            if (!IsServer)
            {
                // os outros: o estado do anfitrião, suavizado
                var r = _tempoRede.Value;
                float k = 1f - Mathf.Exp(-Time.deltaTime / 1.5f);
                Tempo.Nuvens = Mathf.Lerp(Tempo.Nuvens, r.x, k);
                Tempo.Chuva = Mathf.Lerp(Tempo.Chuva, r.y, k);
                Tempo.Vento = Mathf.Lerp(Tempo.Vento, r.z, k);
                Tempo.Pocas = Mathf.Lerp(Tempo.Pocas, r.w, k);
                Tempo.Nivel = (NivelDoTempo)_nivelRede.Value;
                var tr = _tornadoRede.Value;
                Tempo.Tornado = tr.w > 0.001f;
                if (Tempo.Tornado) Tempo.PosicaoDoTornado = Vector3.Lerp(Tempo.PosicaoDoTornado, new Vector3(tr.x, tr.y, tr.z), 1f - Mathf.Exp(-Time.deltaTime / 0.3f));
                Tempo.ForcaDoTornado = tr.w;
                return;
            }

            float dt = Time.deltaTime;
            double horasPorSegundo = 24.0 / (minutosPorDia * 60.0);
            _horas += dt * horasPorSegundo;
            var mundo = MundoDaPartida.Instancia;
            bool semClima = !climaLigado || ModoVideo.Ligado || (mundo != null && mundo.NoDiaZero);
            if (semClima)
            {
                // no Dia 0 (o dia limpo de antes) e no vídeo, nada; o calendário começa depois, com os dias de sol
                _horas = 0;
                _calendario.Clear();
            }
            else if (_calendario.Count == 0 || _calendario[_calendario.Count - 1].fim < _horas + 48) PlanejarCiclo();
            // o que já passou sai do calendário
            while (_calendario.Count > 0 && _calendario[0].fim + 2 < _horas) _calendario.RemoveAt(0);

            // o alvo: o nível forçado (F1), ou o do evento de agora pelo envoltório (as nuvens e o vento chegam antes)
            Vector3 alvo = Vector3.zero;
            var nivel = NivelDoTempo.Sol;
            bool tornadoDoEvento = false;
            float dentroDoEvento = 0f, chegandoATempestade = 0f;
            if (!semClima && _forcado >= 0)
            {
                nivel = (NivelDoTempo)_forcado;
                alvo = Alvo(nivel);
            }
            else if (!semClima && EventoAgora(out var e, out float env, out dentroDoEvento))
            {
                var a = Alvo(e.nivel);
                float chuvaEnv = Mathf.Clamp01((env - 0.45f) / 0.55f);   // a chuva só depois das nuvens e do vento
                alvo = new Vector3(a.x * env, a.y * chuvaEnv, a.z * Mathf.Sqrt(env));
                if (env > 0.5f) nivel = e.nivel;
                tornadoDoEvento = e.tornado;
                // a tempestade chegando: os relâmpagos longe, sem trovão, são o sinal (antes de a chuva vir)
                if (e.nivel >= NivelDoTempo.Tempestade && dentroDoEvento < 0f) chegandoATempestade = env;
            }
            // com o tornado, a tempestade fica inteira até ele passar
            if (_tornado) alvo = Vector3.Max(alvo, Alvo(NivelDoTempo.Raios) + new Vector3(0f, 0f, 0.15f));
            if (_tornado) nivel = NivelDoTempo.Raios;
            float kk = 1f - Mathf.Exp(-dt / 6f);
            _nuvens = Mathf.Lerp(_nuvens, alvo.x, kk);
            _chuva = Mathf.Lerp(_chuva, alvo.y, kk);
            _vento = Mathf.Lerp(_vento, Mathf.Clamp01(alvo.z), kk);
            // as poças: enchem na chuva forte (uma hora do jogo), secam devagar (três horas)
            float horasDt = (float)(dt * horasPorSegundo);
            _pocas = Mathf.Clamp01(_pocas + (_chuva > 0.55f ? horasDt / 1f : -horasDt / 3f));

            Tempo.Nuvens = _nuvens;
            Tempo.Chuva = _chuva;
            Tempo.Vento = _vento;
            Tempo.Pocas = _pocas;
            Tempo.Nivel = nivel;
            if (Time.unscaledTime >= _proximoEnvioDoTempo)
            {
                _proximoEnvioDoTempo = Time.unscaledTime + 1f;
                _tempoRede.Value = new Vector4(_nuvens, _chuva, _vento, _pocas);
                _nivelRede.Value = (byte)nivel;
            }

            // o tornado do evento: desce na metade da tempestade com raios
            if (tornadoDoEvento && !_tornadoDoEventoFeito && dentroDoEvento > 0.35f && !_tornado)
            {
                _tornadoDoEventoFeito = true;
                ComecarTornado();
            }
            if (!tornadoDoEvento && dentroDoEvento <= 0f) _tornadoDoEventoFeito = false;
            Tornado(dt);
            Raios(nivel, chegandoATempestade);
        }

        // ------------------------------------------------------------------------------------------ os raios (no anfitrião)

        // Os três raios (a pedido em 02/11/2026): o que é só o clarão (muito longe: o trovão não chega; é também o sinal da
        // tempestade chegando), o clarão com o trovão longe e o que cai perto.
        void Raios(NivelDoTempo nivel, float chegando)
        {
            float agora = Time.time;
            bool tempestade = nivel >= NivelDoTempo.Tempestade || _tornado;
            bool chuva = nivel == NivelDoTempo.Chuva && _chuva > 0.4f;
            // os de longe (o clarão no céu e o trovão que vem depois): raros na chuva, frequentes na tempestade e mais ainda
            // com o tornado; na tempestade, um em cada quatro é só o clarão
            if ((chuva || tempestade) && agora >= _proximoRaioLonge)
            {
                _proximoRaioLonge = agora + (_tornado ? Random.Range(4f, 9f) : tempestade ? Random.Range(7f, 18f) : Random.Range(60f, 130f));
                if (_proximoRaioLonge - agora < 60f || Random.value < 0.8f) RaioLonge(tempestade && !_tornado && Random.value < 0.25f);
            }
            else if (!(chuva || tempestade)) _proximoRaioLonge = Mathf.Max(_proximoRaioLonge, agora + Random.Range(5f, 20f));
            // a tempestade chegando (antes da chuva): só os clarões, longe, cada vez mais seguidos
            if (!tempestade && chegando > 0.1f && agora >= _proximoRelampago)
            {
                _proximoRelampago = agora + Mathf.Lerp(40f, 12f, chegando) * Random.Range(0.7f, 1.3f);
                RaioLonge(true);
            }
            // os de perto: na tempestade com raios e em volta do tornado
            bool perto = (nivel == NivelDoTempo.Raios && _chuva > 0.6f) || _tornado;
            if (perto && agora >= _proximoRaioPerto)
            {
                _proximoRaioPerto = agora + (_tornado ? Random.Range(4f, 8f) : Random.Range(12f, 30f));
                RaioPerto();
            }
            else if (!perto) _proximoRaioPerto = Mathf.Max(_proximoRaioPerto, agora + Random.Range(6f, 15f));
        }

        float _proximoRelampago;

        static JogadorRede AlguemVivo()
        {
            var vivos = new List<JogadorRede>();
            foreach (var j in JogadorRede.Todos)
                if (j != null && j.IsSpawned && (j.Vida == null || j.Vida.Vida.Value > 0f)) vivos.Add(j);
            return vivos.Count > 0 ? vivos[Random.Range(0, vivos.Count)] : null;
        }

        float AlturaDoChao(Vector3 p)
        {
            var mundo = MundoDaPartida.Instancia;
            if (mundo != null && mundo.Dados != null) return mundo.Dados.Altura(p.x, p.z);
            return Physics.Raycast(new Vector3(p.x, p.y + 300f, p.z), Vector3.down, out var h, 800f, MascaraDoRaio, QueryTriggerInteraction.Ignore) ? h.point.y : p.y;
        }

        /// <summary>O cenário, os carros e o chão: onde o raio cai.</summary>
        const int MascaraDoRaio = (1 << 0) | (1 << 1) | (1 << Vehicles.Carro.CamadaVeiculo);

        /// <summary>
        /// No anfitrião (e pelo F1): um raio longe, a 0,4 a 2,5 km de alguém (o clarão e o trovão longe), ou, com
        /// 'soClarao', a 3 a 6 km (o trovão não chega: o ClimaNaTela só mostra o clarão). Com o tornado, metade cai em volta
        /// dele (o trovão vem de lá).
        /// </summary>
        public void RaioLonge(bool soClarao = false)
        {
            if (!IsServer || !IsSpawned) return;
            var j = AlguemVivo();
            if (j == null) return;
            var dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            var p = j.transform.position + dir * (soClarao ? Random.Range(3000f, 6000f) : Random.Range(400f, 2500f));
            if (_tornado && !soClarao && Random.value < 0.5f) p = _tornadoPos + dir * Random.Range(30f, 160f);
            p.y = AlturaDoChao(p);
            RaioRpc(p, 0);
        }

        /// <summary>
        /// No anfitrião (e pelo F1): um raio perto (a pedido: "cair perto de nós, cair em estruturas ... do lado, nas casas do
        /// lado, na nossa frente, para dar uma sensação de medo"). Cai de 8 a 40 m de alguém, mais na frente, no ponto mais
        /// alto de uns sorteados (o telhado, o poste, o carro, a árvore). Raríssimo: em quem está no aberto, cai nele (mata).
        /// Em volta do tornado, cai perto dele.
        /// </summary>
        public void RaioPerto(bool direto = false)
        {
            if (!IsServer || !IsSpawned) return;
            var j = AlguemVivo();
            if (j == null) return;
            Vector3 centro = j.transform.position, frente = j.transform.forward;
            if (_tornado && Random.value < 0.6f)
            {
                centro = _tornadoPos;
                frente = Random.insideUnitSphere;
            }
            frente.y = 0f;
            frente = frente.sqrMagnitude > 1e-4f ? frente.normalized : Vector3.forward;
            // o raio direto: raríssimo, só em quem está no aberto (sem teto, fora do carro)
            bool aberto = !(j.Passageiro != null && j.Passageiro.Sentado)
                          && !Physics.Raycast(j.transform.position + Vector3.up * 1.2f, Vector3.up, 40f, MascaraDoRaio, QueryTriggerInteraction.Ignore);
            if (direto || (aberto && centro == j.transform.position && Random.value < 0.005f))
            {
                CairRaio(j.transform.position, 2);
                return;
            }
            Vector3 melhor = Vector3.zero;
            float maisAlto = float.MinValue;
            for (int k = 0; k < 8; k++)
            {
                float giro = Random.value < 0.75f ? Random.Range(-75f, 75f) : Random.Range(105f, 255f);
                var p = centro + Quaternion.Euler(0f, giro, 0f) * frente * Random.Range(8f, 40f);
                if (!Physics.Raycast(new Vector3(p.x, centro.y + 250f, p.z), Vector3.down, out var h, 500f, MascaraDoRaio, QueryTriggerInteraction.Ignore)) continue;
                if (h.point.y > maisAlto)
                {
                    maisAlto = h.point.y;
                    melhor = h.point;
                }
            }
            if (maisAlto == float.MinValue) return;
            CairRaio(melhor, 1);
        }

        // o que o raio faz, no anfitrião, e o aviso para todos
        void CairRaio(Vector3 ponto, byte tipo)
        {
            var zm = ZombieManager.Instancia;
            if (zm != null)
                foreach (var z in zm.Ativos)
                {
                    if (z == null || z.Morto) continue;
                    var d = z.transform.position - ponto;
                    if (d.sqrMagnitude > 3.5f * 3.5f) continue;
                    // vira cinza: morre na hora, jogado para longe do raio
                    var dir = (d + Vector3.up * 1.5f).normalized;
                    z.LevarTiro(RegiaoDoCorpo.Tronco, Membro.Nenhum, 1000f, z.transform.position + Vector3.up, dir, true, null, Time.time);
                }
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || j.Vida == null) continue;
                float d = Vector3.Distance(j.transform.position, ponto);
                if (tipo == 2 && d < 2f) j.Vida.Dano(1000f, ponto);   // o raio direto: mata
            }
            Vehicles.PecasDosCarrosDoMundo.Instancia?.RaioNoCarro(ponto);
            RaioRpc(ponto, tipo);
        }

        [Rpc(SendTo.Everyone)]
        void RaioRpc(Vector3 ponto, byte tipo) => Tempo.Avisar(new Raio { ponto = ponto, tipo = tipo });

        // ------------------------------------------------------------------------------------------ o tornado (no anfitrião)

        /// <summary>
        /// No anfitrião (e pelo F1): o tornado desce longe de alguém (a 'distancia', 650 m no calendário) e passa perto dele
        /// (de 20 a 80 m para o lado), seguindo reto até sumir 650 m do outro lado, a uns 4 m/s (uns 5 minutos).
        /// </summary>
        public void ComecarTornado(float distancia = 650f)
        {
            if (!IsServer || !IsSpawned || _tornado) return;
            var j = AlguemVivo();
            if (j == null) return;
            var dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            var lado = Vector3.Cross(Vector3.up, dir) * (Random.value < 0.5f ? -1f : 1f);
            var perto = j.transform.position + lado * Random.Range(20f, 80f);
            _tornadoAntes = Mathf.Max(60f, distancia);
            _tornadoDe = perto - dir * _tornadoAntes;
            _tornadoMeio = perto;
            _tornadoAte = perto + dir * 650f;
            _tornadoT = 0f;
            _tornadoVel = Random.Range(3.5f, 4.6f);
            _tornadoDuracao = (_tornadoAntes + 650f) / _tornadoVel;
            _tornadoForca = 0f;
            _tornadoAcabaEm = -1f;
            _tornado = true;
            _tornadoPos = _tornadoDe;
        }

        /// <summary>No anfitrião (e pelo F1): o tornado se desfaz agora, onde está (em 15 s).</summary>
        public void AcabarTornado()
        {
            if (!IsServer || !_tornado) return;
            if (_tornadoAcabaEm < 0f) _tornadoAcabaEm = Time.time + 15f;
        }

        void Tornado(float dt)
        {
            if (!_tornado)
            {
                if (_tornadoRede.Value.w != 0f) _tornadoRede.Value = Vector4.zero;
                Tempo.Tornado = false;
                Tempo.ForcaDoTornado = 0f;
                return;
            }
            _tornadoT += dt;
            float andado = _tornadoT * _tornadoVel;
            // o caminho: do começo até perto de alguém, e de lá até o fim, reto; com uma leve dança para os lados
            var p = andado < _tornadoAntes ? Vector3.Lerp(_tornadoDe, _tornadoMeio, andado / _tornadoAntes)
                : Vector3.Lerp(_tornadoMeio, _tornadoAte, Mathf.Clamp01((andado - _tornadoAntes) / 650f));
            var dir = (_tornadoAte - _tornadoDe).normalized;
            p += Vector3.Cross(Vector3.up, dir) * (Mathf.Sin(_tornadoT * 0.13f) * 18f + Mathf.Sin(_tornadoT * 0.41f) * 6f);
            p.y = AlturaDoChao(p);
            _tornadoPos = p;
            // desce em 20 s e se desfaz nos últimos 20 s
            _tornadoForca = Mathf.Clamp01(Mathf.Min(_tornadoT / 20f, (_tornadoDuracao - _tornadoT) / 20f));
            if (_tornadoAcabaEm > 0f) _tornadoForca = Mathf.Min(_tornadoForca, (_tornadoAcabaEm - Time.time) / 15f);
            Tempo.Tornado = true;
            Tempo.PosicaoDoTornado = _tornadoPos;
            Tempo.ForcaDoTornado = _tornadoForca;
            if (Time.unscaledTime >= _proximoEnvioDoTornado)
            {
                _proximoEnvioDoTornado = Time.unscaledTime + 0.2f;
                _tornadoRede.Value = new Vector4(p.x, p.y, p.z, Mathf.Max(0.001f, _tornadoForca));
            }
            if (_tornadoT >= _tornadoDuracao || (_tornadoAcabaEm > 0f && Time.time >= _tornadoAcabaEm))
            {
                _tornado = false;
                _tornadoRede.Value = Vector4.zero;
                return;
            }
            if (Time.time < _proximoGolpeDoTornado || _tornadoForca < 0.5f) return;
            _proximoGolpeDoTornado = Time.time + 0.25f;
            // os zumbis perto são arrastados e jogados longe (morrem)
            var zm = ZombieManager.Instancia;
            if (zm != null)
                foreach (var z in zm.Ativos)
                {
                    if (z == null || z.Morto) continue;
                    var d = z.transform.position - p;
                    d.y = 0f;
                    if (d.sqrMagnitude > 15f * 15f) continue;
                    var gira = Vector3.Cross(Vector3.up, d.normalized);
                    z.LevarTiro(RegiaoDoCorpo.Tronco, Membro.Nenhum, 1000f, z.transform.position + Vector3.up, (gira + Vector3.up * 1.2f).normalized, true, null, Time.time);
                }
            // quem fica no miolo dele apanha (a casa protege em parte; o porão e o bunker, por inteiro): o arremesso é do
            // ClimaNaTela, na máquina de cada um
            foreach (var j in JogadorRede.Todos)
            {
                if (j == null || j.Vida == null || j.Vida.Vida.Value <= 0f) continue;
                var d = j.transform.position - p;
                d.y = 0f;
                float protecao = ProtecaoContraOTornado(j.transform.position);
                if (protecao >= 1f || d.magnitude > Mathf.Lerp(13f, 6f, protecao)) continue;
                if (_danoDoTornado.TryGetValue(j, out float ate) && Time.time < ate) continue;
                _danoDoTornado[j] = Time.time + 2.5f;
                j.Vida.Dano(Mathf.Lerp(35f, 15f, protecao), p);
            }
        }

        /// <summary>
        /// O quanto o lugar protege do tornado (0 no aberto, 1 a salvo): dentro de uma casa ou de um prédio, 0,65; debaixo da
        /// terra (o porão, o bunker: mais de 1,2 m abaixo do chão do mundo, com teto), 1.
        /// </summary>
        public float ProtecaoContraOTornado(Vector3 pes)
        {
            bool teto = Physics.Raycast(pes + Vector3.up * 1.2f, Vector3.up, 30f, MascaraDoRaio, QueryTriggerInteraction.Ignore);
            if (teto && pes.y < AlturaDoChao(pes) - 1.2f) return 1f;
            if (AreaInterna.Contem(pes) || teto) return 0.65f;
            return 0f;
        }

        // ------------------------------------------------------------------------------------------ o F1

        /// <summary>No anfitrião (F1): força um nível (-1 volta ao calendário).</summary>
        public void ForcarTempo(int nivel)
        {
            if (!IsServer) return;
            _forcado = nivel;
        }

        public int TempoForcado => _forcado;

        /// <summary>No anfitrião (F1): pula o calendário para o próximo evento (começa já, com os sinais).</summary>
        public void ProximoEventoAgora()
        {
            if (!IsServer || _calendario.Count == 0) return;
            foreach (var e in _calendario)
                if (e.inicio > _horas)
                {
                    _horas = e.inicio - 1.2;
                    return;
                }
        }

        /// <summary>No anfitrião (F1): enche as poças na hora (para testar a pisada).</summary>
        public void EncherPocas()
        {
            if (IsServer) _pocas = 1f;
        }
    }
}
