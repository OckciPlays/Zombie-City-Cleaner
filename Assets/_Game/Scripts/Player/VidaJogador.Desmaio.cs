using Unity.Netcode;
using UnityEngine;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// O desmaio (06/11/2026, a sanidade, a etapa 2: o colapso, abaixo de 15; "algum amigo podendo te acordar"). O dono pede
    /// (o <see cref="Alucinacoes"/> sorteia; na etapa 3, o calmante seguido também), e o anfitrião marca o
    /// <see cref="Desmaiado"/>: o corpo deita para todos (como o caído), os zumbis continuam atacando (ele não é um caído) e
    /// ele acorda sozinho depois de <see cref="DesmaioMinimo"/> a <see cref="DesmaioMaximo"/> s, ou quando um amigo mira
    /// nele e segura o E (o <see cref="TratarOAmigo"/> → <see cref="InventarioJogador.AcordarOAmigo"/>). No dono, o
    /// <see cref="ColapsoDoJogador"/> apaga a tela, tampa o ouvido e tira o controle.
    /// </summary>
    public partial class VidaJogador
    {
        public const float DesmaioMinimo = 4f, DesmaioMaximo = 14f;

        /// <summary>Desmaiado agora (o corpo deitado, sem controle). Só o anfitrião escreve.</summary>
        public readonly NetworkVariable<bool> Desmaiado = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        float _acordaEm;

        /// <summary>No dono: pede para desmaiar por estes segundos (o colapso, o calmante seguido).</summary>
        public void PedirDesmaio(float segundos)
        {
            if (!IsOwner || !IsSpawned || Caido.Value || Desmaiado.Value) return;
            if (_rede != null && _rede.Passageiro != null && _rede.Passageiro.NoCarro) return;
            DesmaiarRpc(segundos);
        }

        [Rpc(SendTo.Server)]
        void DesmaiarRpc(float segundos, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId) return;
            Desmaiar(segundos);
        }

        /// <summary>Só no anfitrião: desmaia por estes segundos (do mínimo ao máximo).</summary>
        public void Desmaiar(float segundos)
        {
            if (!IsServer || !IsSpawned || Caido.Value || Desmaiado.Value || !CorpoAnda) return;
            _acordaEm = Time.time + Mathf.Clamp(segundos, DesmaioMinimo, DesmaioMaximo);
            Desmaiado.Value = true;
        }

        /// <summary>Só no anfitrião: acorda (o tempo passou, ou um amigo acordou).</summary>
        public void Acordar()
        {
            if (!IsServer || !Desmaiado.Value) return;
            Desmaiado.Value = false;
        }

        // no anfitrião, a cada quadro: o tempo do desmaio (e caído não fica desmaiado)
        void AtualizarDesmaio()
        {
            if (!Desmaiado.Value) return;
            if (Caido.Value || Time.time >= _acordaEm) Desmaiado.Value = false;
        }
    }
}
