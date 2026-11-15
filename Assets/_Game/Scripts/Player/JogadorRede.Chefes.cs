using Unity.Netcode;
using UnityEngine;
using ZombieCityCleaner.Zombies;

namespace ZombieCityCleaner.Player
{
    /// <summary>
    /// Os chefes no jogador (15/11/2026; Zumbi.Chefes.cs): o anfitrião decide quem é agarrado e quando é solto; o movimento é
    /// do dono (o anfitrião não move o jogador), então vai para a máquina dele (MovimentoPrimeiraPessoa.Chefes.cs). O
    /// chefe vai por Id e geração (os zumbis não são objetos de rede): o dono acha a cópia dele e a mão que agarra.
    /// <para>
    /// Um chefe por vez: o anfitrião guarda quem segura (<see cref="SeguradoPor"/>) e o soltar diz qual chefe soltou (dois
    /// Agregados agarrando o mesmo jogador, o primeiro a soltar o tirava da mão do outro). Se o dono não pode ir para a mão
    /// (fugindo em pânico, no meio do pulo da janela), ele avisa o anfitrião, e o chefe larga sem apertar nem arremessar.
    /// </para>
    /// </summary>
    public partial class JogadorRede
    {
        /// <summary>No anfitrião: o chefe que segura este jogador agora (null: ninguém).</summary>
        internal Zumbi SeguradoPor { get; private set; }

        bool PresoNumChefe => SeguradoPor != null && SeguradoPor.isActiveAndEnabled && !SeguradoPor.Morto && ReferenceEquals(SeguradoPor.SeguradoPeloChefe, this);

        /// <summary>
        /// No anfitrião: pode ficar (ou continuar) na mão de um chefe: vivo, acordado, fora do carro e do caiaque. Dentro do carro
        /// e do caiaque, ele só leva o golpe.
        /// </summary>
        internal bool LivreParaAMao => IsSpawned && Vida != null && !Vida.Caido.Value && !Vida.Desmaiado.Value && !Vida.Dormindo.Value
                                       && (Passageiro == null || !Passageiro.NoCarro)
                                       && !(TryGetComponent<PassageiroDoCaiaque>(out var caiaque) && caiaque.Caiaque != null);

        /// <summary>No anfitrião: pode ir para a mão de um chefe agora (livre e sem outro chefe segurando).</summary>
        public bool PodeSerAgarrado => LivreParaAMao && !PresoNumChefe;

        /// <summary>Só no anfitrião: o chefe agarrou este jogador (no máximo por este tempo, s).</summary>
        internal void AgarrarPeloChefe(Zumbi chefe, float tempoMaximo)
        {
            if (!IsServer || !IsSpawned || chefe == null) return;
            SeguradoPor = chefe;
            AgarradoPeloChefeRpc((ushort)chefe.Id, chefe.Geracao, tempoMaximo);
        }

        /// <summary>Só no anfitrião: este chefe soltou o jogador (com a velocidade do arremesso; zero, só larga).</summary>
        internal void SoltarDoChefe(Zumbi chefe, Vector3 velocidade)
        {
            if (!IsServer || chefe == null) return;
            if (ReferenceEquals(SeguradoPor, chefe)) SeguradoPor = null;
            if (IsSpawned) SoltoPeloChefeRpc((ushort)chefe.Id, chefe.Geracao, velocidade);   // (quem saiu do jogo não recebe nada)
        }

        [Rpc(SendTo.Owner)]
        void AgarradoPeloChefeRpc(ushort id, byte geracao, float tempoMaximo)
        {
            var m = GetComponent<MovimentoPrimeiraPessoa>();
            var zm = ZombieManager.Instancia;
            var z = zm != null ? zm.PorId(id, geracao) : null;
            if (m != null && z != null && z.maoQueAgarra != null && m.AgarrarPeloChefe(z, z.maoQueAgarra, tempoMaximo))
            {
                InventarioJogador.AvisoLocal($"{z.NomeDoChefe} te agarrou! Atirem nele para ele soltar.");
                return;
            }
            RecusouOAgarraoRpc(id, geracao);   // (não deu para ir para a mão: o chefe larga)
        }

        [Rpc(SendTo.Owner)]
        void SoltoPeloChefeRpc(ushort id, byte geracao, Vector3 velocidade)
        {
            var m = GetComponent<MovimentoPrimeiraPessoa>();
            if (m == null) return;
            // só o chefe que segura solta (um outro que tinha agarrado antes não tira o jogador da mão deste)
            var quem = m.ChefeQueSegura;
            if (quem != null && (quem.Id != id || quem.Geracao != geracao)) return;
            m.SoltarDoChefe(velocidade);
        }

        [Rpc(SendTo.Server)]
        void RecusouOAgarraoRpc(ushort id, byte geracao, RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId) return;
            var zm = ZombieManager.Instancia;
            var z = zm != null ? zm.PorId(id, geracao) : null;
            if (z != null) z.LargarSemArremesso(this);
            if (ReferenceEquals(SeguradoPor, z)) SeguradoPor = null;
        }
    }
}
