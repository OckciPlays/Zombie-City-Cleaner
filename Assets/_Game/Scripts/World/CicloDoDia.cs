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
    public class CicloDoDia : NetworkBehaviour
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
        /// <summary>0 = dia claro (8h às 17h), 1 = noite fechada (20h às 5h); o resto é amanhecer e anoitecer.</summary>
        public static float Noite { get; private set; } = 1f;
        /// <summary>O relógio está parado (a hora não anda).</summary>
        public static bool Parado { get; private set; }

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
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                HoraRede.Value = Hora;
                ParadoRede.Value = false;   // sem a cena recarregar, cada partida começa com o relógio andando
            }
            else Hora = HoraRede.Value;
            HoraRede.OnValueChanged += AoReceber;
        }

        public override void OnNetworkDespawn()
        {
            HoraRede.OnValueChanged -= AoReceber;
            Parado = false;
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

        void Update()
        {
            if (IsSpawned) Parado = ParadoRede.Value;
            if (!Parado) Hora = Mathf.Repeat(Hora + 24f / (minutosPorDia * 60f) * Time.deltaTime, 24f);
            bool mandaNaHora = !IsSpawned || IsServer;
            var teclado = Keyboard.current;
            if (mandaNaHora && teclado != null)
            {
                if (teclado.pageUpKey.wasPressedThisFrame) Hora = Mathf.Repeat(Hora + 1f, 24f);
                if (teclado.pageDownKey.wasPressedThisFrame) Hora = Mathf.Repeat(Hora - 1f, 24f);
            }
            if (IsSpawned && IsServer && Time.unscaledTime >= _proximoEnvio)
            {
                HoraRede.Value = Hora;
                _proximoEnvio = Time.unscaledTime + 1f;
            }
            Noite = CalcularNoite(Hora);
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
