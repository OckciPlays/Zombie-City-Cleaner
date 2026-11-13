using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// A hora do dia. O anfitrião manda: a hora anda no servidor e vai para os clientes a cada segundo (entre um envio
    /// e outro, cada um avança sozinho). Fora da partida (no menu) o relógio roda localmente.
    /// Teste: o anfitrião avança ou volta 1 hora com Page Up / Page Down; "-hora 22" na linha de comando muda a hora inicial;
    /// o "Sempre de dia" e o "Sempre de noite" do menu do desenvolvedor (F1) põem meio-dia ou meia-noite e param o relógio
    /// (<see cref="SempreDeDia"/>, <see cref="SempreDeNoite"/>).
    /// </summary>
    public partial class CicloDoDia : NetworkBehaviour
    {
        [Tooltip("Hora quando o jogo abre (0 a 24).")]
        public float horaInicial = 21f;
        [Tooltip("Minutos reais para um dia inteiro (24 horas do jogo).")]
        public float minutosPorDia = 24f;

        public readonly NetworkVariable<float> HoraRede = new(21f);
        /// <summary>O relógio parado (o "Sempre de dia" ou o "Sempre de noite" do F1). Escrito pelo anfitrião.</summary>
        public readonly NetworkVariable<bool> ParadoRede = new(false);

        public static CicloDoDia Instancia { get; private set; }
        /// <summary>Hora atual, de 0 a 24.</summary>
        public static float Hora { get; private set; } = 21f;
        /// <summary>
        /// O dia do mundo (1 no começo; mais um a cada meia-noite), do anfitrião (02/11/2026, o save: a lista do Continuar, e
        /// depois a reposição dos comerciantes).
        /// </summary>
        public static int Dia { get; private set; } = 1;
        public readonly NetworkVariable<int> DiaRede = new(1);
        /// <summary>0 = dia claro (8h às 17h), 1 = noite fechada (20h às 5h); o resto é amanhecer e anoitecer.</summary>
        public static float Noite { get; private set; } = 1f;
        /// <summary>O relógio está parado (a hora não anda).</summary>
        public static bool Parado { get; private set; }
        /// <summary>
        /// Quantas horas do jogo passam por segundo real (05/11/2026, o corpo do jogador: as feridas e as lesões contam em
        /// horas vividas, não pelo relógio do mundo, que o Page Up e o Sempre de dia pulam). Com 24 minutos por dia, 1 h do
        /// jogo = 60 s reais.
        /// </summary>
        public static float HorasPorSegundo => 24f / ((Instancia != null ? Instancia.minutosPorDia : 24f) * 60f) * Pressa;

        /// <summary>
        /// Quantas vezes o relógio corre (06/11/2026, a mente: todos dormindo, a noite passa: <c>VidaJogador.PressaDoSono</c>);
        /// 1, normal. Vai pela rede; as horas do corpo e da mente correm junto (<see cref="HorasPorSegundo"/>).
        /// </summary>
        public static float Pressa { get; private set; } = 1f;
        public readonly NetworkVariable<float> PressaRede = new(1f);

        /// <summary>Só no anfitrião: a pressa do relógio.</summary>
        public static void DefinirPressa(float pressa)
        {
            pressa = Mathf.Max(1f, pressa);
            if (Mathf.Approximately(Pressa, pressa)) return;
            Pressa = pressa;
            var c = Instancia;
            if (c != null && c.IsSpawned && c.IsServer) c.PressaRede.Value = pressa;
        }

        /// <summary>
        /// O relógio rápido do F1 (06/11/2026, a parte 6 do Plano_NPCs_Bunker.md da pasta de arte: ×10 e ×60, para testar a
        /// agenda dos moradores), no anfitrião; os outros recebem a hora a cada segundo. Volta a 1 a cada partida.
        /// </summary>
        public static float Acelerado = 1f;

        float _proximoEnvio;

        void Awake()
        {
            Instancia = this;
            Parado = false;
            Hora = horaInicial;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-hora" && float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float h))
                    Hora = Mathf.Repeat(h, 24f);
            Noite = CalcularNoite(Hora);
            // o clima (o tempo: a chuva, os raios e o tornado; CicloDoDia.Tempo.cs) e o que ele mostra e toca nesta máquina
            if (GetComponent<ClimaNaTela>() == null) gameObject.AddComponent<ClimaNaTela>();
        }

        public override void OnNetworkSpawn()
        {
            Pressa = 1f;   // (06/11/2026: a pressa do sono não passa de uma partida para outra)
            TempoAoComecar();
            Acelerado = 1f;
            if (IsServer)
            {
                HoraRede.Value = Hora;
                ParadoRede.Value = false;   // sem a cena recarregar, cada partida começa com o relógio andando
                Dia = 1;
                DiaRede.Value = 1;
            }
            else
            {
                Hora = HoraRede.Value;
                Dia = DiaRede.Value;
            }
            HoraRede.OnValueChanged += AoReceber;
        }

        /// <summary>No anfitrião: a hora e o dia do mundo salvo.</summary>
        public void Definir(float hora, int dia)
        {
            if (IsSpawned && !IsServer) return;
            Hora = Mathf.Repeat(hora, 24f);
            Dia = Mathf.Max(1, dia);
            if (!IsSpawned) return;
            HoraRede.Value = Hora;
            DiaRede.Value = Dia;
        }

        public override void OnNetworkDespawn()
        {
            HoraRede.OnValueChanged -= AoReceber;
            Parado = false;
            Pressa = 1f;
            TempoAoAcabar();
        }

        public override void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
            base.OnDestroy();
        }

        void AoReceber(float antes, float depois)
        {
            if (!IsServer) Hora = depois;
        }

        /// <summary>No anfitrião: põe meio-dia e para o relógio, ou solta o relógio de novo.</summary>
        public void SempreDeDia(bool ligado) => Parar(ligado, 12f);

        /// <summary>No anfitrião: põe meia-noite e para o relógio, ou solta o relógio de novo.</summary>
        public void SempreDeNoite(bool ligado) => Parar(ligado, 0f);

        void Parar(bool ligado, float hora)
        {
            if (IsSpawned && !IsServer) return;
            if (IsSpawned) ParadoRede.Value = ligado;
            Parado = ligado;
            if (!ligado) return;
            Hora = hora;
            if (IsSpawned) HoraRede.Value = Hora;
        }

        /// <summary>
        /// Quantos amanheceres (as 6 h) passaram nesta partida, no anfitrião (05/11/2026, a infecção do corpo: o sorteio é ao
        /// amanhecer). Conta o relógio andando e o Page Up; não conta o Page Down, o save nem a saída do Dia 0.
        /// </summary>
        public static int Amanheceres { get; private set; }

        void Update()
        {
            float h0 = Hora;
            if (IsSpawned) Parado = ParadoRede.Value;
            if (IsSpawned && !IsServer) Pressa = Mathf.Max(1f, PressaRede.Value);
            if (!Parado)
            {
                float antes = Hora;
                // a pressa do sono (todos dormindo: a noite passa) e o relógio rápido do F1 BUNKER (só no anfitrião;
                // os outros recebem a hora a cada segundo)
                float rapido = !IsSpawned || IsServer ? Mathf.Max(1f, Acelerado) : 1f;
                Hora = Mathf.Repeat(Hora + 24f / (minutosPorDia * 60f) * Pressa * rapido * Time.deltaTime, 24f);
                // passou da meia-noite: mais um dia (no anfitrião; os outros recebem)
                if (Hora < antes && (!IsSpawned || IsServer)) Dia++;
            }
            if (IsSpawned && !IsServer) Dia = DiaRede.Value;
            bool mandaNaHora = !IsSpawned || IsServer;
            var teclado = Keyboard.current;
            if (mandaNaHora && teclado != null)
            {
                if (teclado.pageUpKey.wasPressedThisFrame) Hora = Mathf.Repeat(Hora + 1f, 24f);
                if (teclado.pageDownKey.wasPressedThisFrame) Hora = Mathf.Repeat(Hora - 1f, 24f);
            }
            if (mandaNaHora)
            {
                // as 6 h ficaram entre a hora de antes e a de agora, andando para a frente (menos de 12 h de pulo)
                float d = Mathf.Repeat(Hora - h0, 24f), a = Mathf.Repeat(6f - h0, 24f);
                if (d > 0f && d < 12f && a > 0f && a <= d) Amanheceres++;
            }
            if (IsSpawned && IsServer && Time.unscaledTime >= _proximoEnvio)
            {
                HoraRede.Value = Hora;
                if (DiaRede.Value != Dia) DiaRede.Value = Dia;
                _proximoEnvio = Time.unscaledTime + 1f;
            }
            Noite = CalcularNoite(Hora);
            AtualizarTempo();
        }

        public static float CalcularNoite(float h)
        {
            if (h >= 8f && h <= 17f) return 0f;
            if (h >= 20f || h <= 5f) return 1f;
            return h > 17f ? Mathf.SmoothStep(0f, 1f, (h - 17f) / 3f) : Mathf.SmoothStep(1f, 0f, (h - 5f) / 3f);
        }

        /// <summary>Muda a hora (no anfitrião; a cena de vídeo usa para cada tomada). Ela vai para os clientes no próximo envio.</summary>
        public static void MudarHora(float hora) => Hora = Mathf.Repeat(hora, 24f);

        public static string Relogio() => $"{Mathf.FloorToInt(Hora):00}:{Mathf.FloorToInt(Hora % 1f * 60f):00}";
    }
}
