using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombieCityCleaner.World
{
    /// <summary>
    /// A hora do dia. O anfitrião manda: a hora anda no servidor e vai para os clientes a cada segundo (entre um envio
    /// e outro, cada um avança sozinho). Fora da partida (no menu) o relógio roda localmente.
    /// Teste: o anfitrião avança ou volta 1 hora com Page Up / Page Down; "-hora 22" na linha de comando muda a hora inicial.
    /// </summary>
    public class CicloDoDia : NetworkBehaviour
    {
        [Tooltip("Hora quando o jogo abre (0 a 24).")]
        public float horaInicial = 21f;
        [Tooltip("Minutos reais para um dia inteiro (24 horas do jogo).")]
        public float minutosPorDia = 24f;

        public readonly NetworkVariable<float> HoraRede = new(21f);

        /// <summary>Hora atual, de 0 a 24.</summary>
        public static float Hora { get; private set; } = 21f;
        /// <summary>0 = dia claro (8h às 17h), 1 = noite fechada (20h às 5h); o resto é amanhecer e anoitecer.</summary>
        public static float Noite { get; private set; } = 1f;

        float _proximoEnvio;

        void Awake()
        {
            Hora = horaInicial;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-hora" && float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float h))
                    Hora = Mathf.Repeat(h, 24f);
            Noite = CalcularNoite(Hora);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) HoraRede.Value = Hora;
            else Hora = HoraRede.Value;
            HoraRede.OnValueChanged += AoReceber;
        }

        public override void OnNetworkDespawn() => HoraRede.OnValueChanged -= AoReceber;

        void AoReceber(float antes, float depois)
        {
            if (!IsServer) Hora = depois;
        }

        void Update()
        {
            Hora = Mathf.Repeat(Hora + 24f / (minutosPorDia * 60f) * Time.deltaTime, 24f);
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

        public static string Relogio() => $"{Mathf.FloorToInt(Hora):00}:{Mathf.FloorToInt(Hora % 1f * 60f):00}";
    }
}
